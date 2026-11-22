using System.Threading.Tasks;

namespace Deterpos.Application.Interfaces;

public class DatabaseConnectionParams
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = "deterpos_db";
    public string Username { get; set; } = "postgres";
    public string Password { get; set; } = "postgres";
}

public class ConnectionTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ServerVersion { get; set; }
    public int TablesCount { get; set; }
}

public class BackupOperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
}

public interface IDatabaseBackupService
{
    DatabaseConnectionParams GetCurrentConnectionParams();
    Task<ConnectionTestResult> TestConnectionAsync(DatabaseConnectionParams connectionParams);
    Task<BackupOperationResult> CreateBackupAsync(string targetFilePath, DatabaseConnectionParams? connectionParams = null);
    Task<BackupOperationResult> RestoreBackupAsync(string sourceFilePath, DatabaseConnectionParams? connectionParams = null);
}
