using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Deterpos.Application.Interfaces;

namespace Deterpos.Infrastructure.Services;

public class BcvExchangeRateService : IBcvExchangeRateService
{
    private static readonly HttpClient _httpClient;

    static BcvExchangeRateService()
    {
        var handler = new HttpClientHandler
        {
            // Omitir errores de certificado SSL únicamente para el portal gubernamental del BCV (bcv.org.ve)
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
            {
                if (errors == System.Net.Security.SslPolicyErrors.None) return true;
                return message.RequestUri != null && message.RequestUri.Host.EndsWith("bcv.org.ve", StringComparison.OrdinalIgnoreCase);
            }
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(6)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Deterpos/2.0");
    }

    public async Task<BcvExchangeRateResult> FetchOfficialRatesAsync(CancellationToken cancellationToken = default)
    {
        var result = new BcvExchangeRateResult();

        // 1. Intentar consultar API DolarApi Venezuela (Servidor de alta disponibilidad para BCV)
        try
        {
            var usdResponse = await _httpClient.GetStringAsync("https://ve.dolarapi.com/v1/dolares/oficial", cancellationToken);
            using var usdDoc = JsonDocument.Parse(usdResponse);
            var root = usdDoc.RootElement;

            if (root.TryGetProperty("promedio", out var promedioElem) && promedioElem.GetDecimal() > 0)
            {
                result.VesRate = decimal.Round(promedioElem.GetDecimal(), 4);
                result.Source = "Banco Central de Venezuela (Oficial BCV)";
                result.IsSuccess = true;

                if (root.TryGetProperty("fechaActualizacion", out var fechaElem) && fechaElem.TryGetDateTime(out var dt))
                {
                    result.EffectiveDate = dt;
                }
            }

            // Consultar Euro BCV
            try
            {
                var eurResponse = await _httpClient.GetStringAsync("https://ve.dolarapi.com/v1/euros/oficial", cancellationToken);
                using var eurDoc = JsonDocument.Parse(eurResponse);
                if (eurDoc.RootElement.TryGetProperty("promedio", out var eurPromedio) && eurPromedio.GetDecimal() > 0)
                {
                    // Tasa EUR relativa a USD (1 EUR en USD o EUR/USD)
                    decimal eurBcvVes = eurPromedio.GetDecimal();
                    if (result.VesRate > 0)
                    {
                        // Si 1 USD = 55.40 VES y 1 EUR = 58.20 VES, entonces 1 EUR = 58.20 / 55.40 USD = 1.0505 USD
                        result.EurRate = decimal.Round(eurBcvVes / result.VesRate, 4);
                    }
                    else
                    {
                        result.EurRate = 0.9200m;
                    }
                }
            }
            catch
            {
                result.EurRate = 0.9200m;
            }
        }
        catch
        {
            // Si la API falla, ejecutar Estrategia 2: Scraping directo al portal web del BCV
            try
            {
                var bcvHtml = await _httpClient.GetStringAsync("https://www.bcv.org.ve/", cancellationToken);

                // Regex para extraer tasa del dólar en el HTML oficial
                var matchDolar = Regex.Match(bcvHtml, @"id=[""']dolar[""'][\s\S]*?<strong>\s*([0-9]+[.,][0-9]+)\s*<\/strong>", RegexOptions.IgnoreCase);
                if (matchDolar.Success)
                {
                    string rawVal = matchDolar.Groups[1].Value.Replace(',', '.');
                    if (decimal.TryParse(rawVal, NumberStyles.Any, CultureInfo.InvariantCulture, out var rate) && rate > 0)
                    {
                        result.VesRate = decimal.Round(rate, 4);
                        result.Source = "Banco Central de Venezuela (Directo Portal BCV)";
                        result.IsSuccess = true;
                        result.EffectiveDate = DateTime.UtcNow;
                    }
                }
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"No se pudo contactar al BCV: {ex.Message}";
            }
        }

        // Consultar tasa COP (Peso Colombiano)
        try
        {
            var copResponse = await _httpClient.GetStringAsync("https://open.er-api.com/v6/latest/USD", cancellationToken);
            using var copDoc = JsonDocument.Parse(copResponse);
            if (copDoc.RootElement.TryGetProperty("rates", out var rates) &&
                rates.TryGetProperty("COP", out var copElem) && copElem.GetDecimal() > 0)
            {
                result.CopRate = decimal.Round(copElem.GetDecimal(), 2);
            }
            else
            {
                result.CopRate = 3345.00m;
            }
        }
        catch
        {
            result.CopRate = 3345.00m;
        }

        if (result.EurRate <= 0) result.EurRate = 0.9200m;

        return result;
    }
}
