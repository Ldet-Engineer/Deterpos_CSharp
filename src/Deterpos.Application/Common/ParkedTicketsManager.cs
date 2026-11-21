using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Deterpos.Domain.Enums;

namespace Deterpos.Application.Common;

public class ParkedCartItemDraft
{
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal UnitCostUSD { get; set; }
    public decimal UnitPriceUSD { get; set; }
    public decimal Quantity { get; set; }
    public VatRateType VatRate { get; set; } = VatRateType.General16;
    public decimal TotalPriceUSD => decimal.Round(Quantity * UnitPriceUSD, 4);
}

public class ParkedTicket
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();
    public string Reference { get; set; } = string.Empty;
    public DateTime ParkedAt { get; set; } = DateTime.UtcNow;
    public int? CustomerId { get; set; }
    public string CustomerDocument { get; set; } = "V-00000000";
    public string CustomerName { get; set; } = "Cliente de Contado / Genérico";
    public decimal AppliedRateVES { get; set; } = 55.40m;
    public List<ParkedCartItemDraft> Items { get; set; } = new();

    public decimal TotalUSD => Items.Sum(i => i.TotalPriceUSD);
    public decimal TotalVES => decimal.Round(TotalUSD * AppliedRateVES, 2);
    public decimal TotalPieces => Items.Sum(i => i.Quantity);
    public string SummaryDisplay => $"{Items.Count} renglones ({TotalPieces:G29} uds) • Ref. ${TotalUSD:N2} ({TotalVES:N2} Bs.)";
    public string TimeDisplay => ParkedAt.ToLocalTime().ToString("hh:mm tt");
}

public static class ParkedTicketsManager
{
    private static readonly List<ParkedTicket> _tickets = new();
    private static readonly string _storagePath;

    public static event Action? ParkedTicketsChanged;

    static ParkedTicketsManager()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "Deterpos");
        Directory.CreateDirectory(dir);
        _storagePath = Path.Combine(dir, "parked_tickets.json");
        LoadFromDisk();
    }

    public static IReadOnlyList<ParkedTicket> GetAll()
    {
        lock (_tickets)
        {
            return _tickets.OrderByDescending(t => t.ParkedAt).ToList();
        }
    }

    public static int Count
    {
        get
        {
            lock (_tickets)
            {
                return _tickets.Count;
            }
        }
    }

    public static ParkedTicket ParkCart(string reference, int? customerId, string customerDoc, string customerName, decimal rate, List<ParkedCartItemDraft> items)
    {
        var ticket = new ParkedTicket
        {
            Reference = string.IsNullOrWhiteSpace(reference) ? $"Ticket #{_tickets.Count + 1}" : reference,
            CustomerId = customerId,
            CustomerDocument = customerDoc,
            CustomerName = customerName,
            AppliedRateVES = rate,
            Items = items.ToList()
        };

        lock (_tickets)
        {
            _tickets.Add(ticket);
            SaveToDisk();
        }

        ParkedTicketsChanged?.Invoke();
        return ticket;
    }

    public static ParkedTicket? Restore(string id)
    {
        ParkedTicket? found;
        lock (_tickets)
        {
            found = _tickets.FirstOrDefault(t => t.Id == id);
            if (found != null)
            {
                _tickets.Remove(found);
                SaveToDisk();
            }
        }

        if (found != null)
        {
            ParkedTicketsChanged?.Invoke();
        }

        return found;
    }

    public static bool Discard(string id)
    {
        bool removed;
        lock (_tickets)
        {
            var found = _tickets.FirstOrDefault(t => t.Id == id);
            removed = found != null && _tickets.Remove(found);
            if (removed)
            {
                SaveToDisk();
            }
        }

        if (removed)
        {
            ParkedTicketsChanged?.Invoke();
        }

        return removed;
    }

    public static void ClearAll()
    {
        lock (_tickets)
        {
            _tickets.Clear();
            SaveToDisk();
        }
        ParkedTicketsChanged?.Invoke();
    }

    private static void SaveToDisk()
    {
        try
        {
            var json = JsonSerializer.Serialize(_tickets, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_storagePath, json);
        }
        catch
        {
            // Fallback seguro en memoria si hay bloqueo temporal de disco
        }
    }

    private static void LoadFromDisk()
    {
        try
        {
            if (File.Exists(_storagePath))
            {
                var json = File.ReadAllText(_storagePath);
                var loaded = JsonSerializer.Deserialize<List<ParkedTicket>>(json);
                if (loaded != null)
                {
                    _tickets.Clear();
                    _tickets.AddRange(loaded);
                }
            }
        }
        catch
        {
            // Ignorar y comenzar con lista limpia
        }
    }
}
