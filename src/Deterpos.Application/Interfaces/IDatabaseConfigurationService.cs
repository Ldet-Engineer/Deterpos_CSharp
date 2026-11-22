namespace Deterpos.Application.Interfaces;

public class DatabaseSettings
{
    public string Server { get; set; } = "localhost";
    [System.Text.Json.Serialization.JsonIgnore]
    public string Host { get => Server; set => Server = value; }
    public int Port { get; set; } = 5432;
    public string DatabaseName { get; set; } = "deterpos_db";
    [System.Text.Json.Serialization.JsonIgnore]
    public string Database { get => DatabaseName; set => DatabaseName = value; }
    public string Username { get; set; } = "postgres";
    public string Password { get; set; } = "postgres";
    public string TerminalIdentifier { get; set; } = "POS-01";
    public string TerminalName { get; set; } = "Caja Principal - Servidor";

    public string BuildConnectionString()
    {
        return $"Host={Server};Port={Port};Database={DatabaseName};Username={Username};Password={Password};Include Error Detail=true";
    }
}

public interface IDatabaseConfigurationService
{
    DatabaseSettings LoadSettings();
    void SaveSettings(DatabaseSettings settings);
    Task<(bool Success, string Message)> TestConnectionAsync(DatabaseSettings settings);
    Task<(bool Success, string Message)> InitializeDatabaseAsync();
}
