using System.Text.Json;
using Deterpos.Application.Interfaces;
using Deterpos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Deterpos.Infrastructure.Services;

public class DatabaseConfigurationService : IDatabaseConfigurationService
{
    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Deterpos"
    );

    private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "dbsettings.json");

    public DatabaseSettings LoadSettings()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                var settings = JsonSerializer.Deserialize<DatabaseSettings>(json);
                if (settings != null) return settings;
            }
        }
        catch
        {
            // Si hay error leyendo el archivo, retornamos valores por defecto
        }

        return new DatabaseSettings();
    }

    public void SaveSettings(DatabaseSettings settings)
    {
        if (!Directory.Exists(ConfigDirectory))
        {
            Directory.CreateDirectory(ConfigDirectory);
        }

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigFilePath, json);
    }

    public async Task<(bool Success, string Message)> TestConnectionAsync(DatabaseSettings settings)
    {
        try
        {
            var connString = settings.BuildConnectionString();
            await using var connection = new NpgsqlConnection(connString);
            await connection.OpenAsync();
            return (true, "Conexión a PostgreSQL establecida exitosamente.");
        }
        catch (Exception ex)
        {
            return (false, $"Error al conectar a PostgreSQL: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> InitializeDatabaseAsync()
    {
        try
        {
            var settings = LoadSettings();
            var optionsBuilder = new DbContextOptionsBuilder<DeterposDbContext>();
            optionsBuilder.UseNpgsql(settings.BuildConnectionString());

            await using var context = new DeterposDbContext(optionsBuilder.Options);
            // Crea la base de datos y todas las tablas si no existen con sus datos semilla
            var created = await context.Database.EnsureCreatedAsync();

            return (true, created ? "Base de datos creada y configurada correctamente con datos iniciales." : "Base de datos existente verificada correctamente.");
        }
        catch (Exception ex)
        {
            return (false, $"Error al inicializar la base de datos: {ex.Message}");
        }
    }
}
