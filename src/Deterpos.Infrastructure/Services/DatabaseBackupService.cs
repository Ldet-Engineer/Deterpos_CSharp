using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Deterpos.Application.Interfaces;
using Npgsql;

namespace Deterpos.Infrastructure.Services;

public class DatabaseBackupService : IDatabaseBackupService
{
    private readonly DatabaseConfigurationService _configService;

    public DatabaseBackupService()
    {
        _configService = new DatabaseConfigurationService();
    }

    public DatabaseBackupService(DatabaseConfigurationService configService)
    {
        _configService = configService;
    }

    public DatabaseConnectionParams GetCurrentConnectionParams()
    {
        var settings = _configService.LoadSettings();
        return new DatabaseConnectionParams
        {
            Host = settings.Host,
            Port = settings.Port,
            Database = settings.Database,
            Username = settings.Username,
            Password = settings.Password
        };
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(DatabaseConnectionParams connectionParams)
    {
        var connStr = $"Host={connectionParams.Host};Port={connectionParams.Port};Database={connectionParams.Database};Username={connectionParams.Username};Password={connectionParams.Password};Timeout=5;Command Timeout=5";

        try
        {
            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();

            string? serverVersion = null;
            await using (var cmd = new NpgsqlCommand("SELECT version();", conn))
            {
                var ver = await cmd.ExecuteScalarAsync();
                serverVersion = ver?.ToString();
            }

            int tableCount = 0;
            await using (var cmd = new NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public';", conn))
            {
                var count = await cmd.ExecuteScalarAsync();
                if (count != null && int.TryParse(count.ToString(), out int tc))
                {
                    tableCount = tc;
                }
            }

            return new ConnectionTestResult
            {
                Success = true,
                Message = "Conexión a PostgreSQL establecida con éxito.",
                ServerVersion = serverVersion,
                TablesCount = tableCount
            };
        }
        catch (Exception ex)
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"Fallo al conectar a PostgreSQL: {ex.Message}",
                ServerVersion = null,
                TablesCount = 0
            };
        }
    }

    public async Task<BackupOperationResult> CreateBackupAsync(string targetFilePath, DatabaseConnectionParams? connectionParams = null)
    {
        var p = connectionParams ?? GetCurrentConnectionParams();

        try
        {
            // Asegurar directorio destino
            var dir = Path.GetDirectoryName(targetFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // 1. Buscar pg_dump.exe en instalación local nativa de Windows
            string? pgDumpPath = FindPostgreSqlBinary("pg_dump.exe");

            if (!string.IsNullOrEmpty(pgDumpPath) && File.Exists(pgDumpPath))
            {
                // Respaldo mediante pg_dump.exe nativo
                var psi = new ProcessStartInfo
                {
                    FileName = pgDumpPath,
                    Arguments = $"-h {p.Host} -p {p.Port} -U {p.Username} -F p -b -v -f \"{targetFilePath}\" {p.Database}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.EnvironmentVariables["PGPASSWORD"] = p.Password;

                using var process = Process.Start(psi);
                if (process != null)
                {
                    string stderr = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0 && File.Exists(targetFilePath) && new FileInfo(targetFilePath).Length > 0)
                    {
                        var info = new FileInfo(targetFilePath);
                        return new BackupOperationResult
                        {
                            Success = true,
                            Message = $"Respaldo generado exitosamente con pg_dump nativo.\nTamaño: {info.Length / 1024.0:N1} KB",
                            FilePath = targetFilePath,
                            FileSizeBytes = info.Length
                        };
                    }
                }
            }

            // 2. Fallback: Si no hay pg_dump nativo, verificar si PostgreSQL corre en Docker (entorno dev)
            string? dockerContainer = await GetDockerPostgresContainerNameAsync();
            if (!string.IsNullOrEmpty(dockerContainer))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"exec {dockerContainer} pg_dump -U {p.Username} -F p {p.Database}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    using (var fs = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var sw = new StreamWriter(fs))
                    {
                        string output = await process.StandardOutput.ReadToEndAsync();
                        await sw.WriteAsync(output);
                    }

                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0 && File.Exists(targetFilePath) && new FileInfo(targetFilePath).Length > 0)
                    {
                        var info = new FileInfo(targetFilePath);
                        return new BackupOperationResult
                        {
                            Success = true,
                            Message = $"Copia de seguridad generada con éxito desde Docker ({dockerContainer}).\nTamaño: {info.Length / 1024.0:N1} KB",
                            FilePath = targetFilePath,
                            FileSizeBytes = info.Length
                        };
                    }
                }
            }

            // 3. Fallback C#: Volcado SQL estructurado vía conexión Npgsql directa
            return await CreateDirectSqlDumpAsync(targetFilePath, p);
        }
        catch (Exception ex)
        {
            return new BackupOperationResult
            {
                Success = false,
                Message = $"Error al generar respaldo de la base de datos: {ex.Message}",
                FilePath = targetFilePath,
                FileSizeBytes = 0
            };
        }
    }

    public async Task<BackupOperationResult> RestoreBackupAsync(string sourceFilePath, DatabaseConnectionParams? connectionParams = null)
    {
        if (!File.Exists(sourceFilePath))
        {
            return new BackupOperationResult
            {
                Success = false,
                Message = $"El archivo de respaldo no fue encontrado en: {sourceFilePath}",
                FilePath = sourceFilePath,
                FileSizeBytes = 0
            };
        }

        var p = connectionParams ?? GetCurrentConnectionParams();

        try
        {
            string? psqlPath = FindPostgreSqlBinary("psql.exe");

            if (!string.IsNullOrEmpty(psqlPath) && File.Exists(psqlPath))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = psqlPath,
                    Arguments = $"-h {p.Host} -p {p.Port} -U {p.Username} -d {p.Database} -f \"{sourceFilePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.EnvironmentVariables["PGPASSWORD"] = p.Password;

                using var process = Process.Start(psi);
                if (process != null)
                {
                    string stderr = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        return new BackupOperationResult
                        {
                            Success = true,
                            Message = "Base de datos restaurada con éxito mediante psql nativo.",
                            FilePath = sourceFilePath,
                            FileSizeBytes = new FileInfo(sourceFilePath).Length
                        };
                    }
                }
            }

            // Fallback Docker
            string? dockerContainer = await GetDockerPostgresContainerNameAsync();
            if (!string.IsNullOrEmpty(dockerContainer))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -Command \"Get-Content -Raw '{sourceFilePath}' | docker exec -i {dockerContainer} psql -U {p.Username} -d {p.Database}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    if (process.ExitCode == 0)
                    {
                        return new BackupOperationResult
                        {
                            Success = true,
                            Message = $"Base de datos restaurada con éxito en el contenedor Docker ({dockerContainer}).",
                            FilePath = sourceFilePath,
                            FileSizeBytes = new FileInfo(sourceFilePath).Length
                        };
                    }
                }
            }

            // Fallback ejecución SQL directa
            var sqlContent = await File.ReadAllTextAsync(sourceFilePath);
            var connStr = $"Host={p.Host};Port={p.Port};Database={p.Database};Username={p.Username};Password={p.Password};Timeout=60";
            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = new NpgsqlCommand(sqlContent, conn);
            cmd.CommandTimeout = 120;
            await cmd.ExecuteNonQueryAsync();

            return new BackupOperationResult
            {
                Success = true,
                Message = "Base de datos restaurada con éxito mediante conexión directa.",
                FilePath = sourceFilePath,
                FileSizeBytes = new FileInfo(sourceFilePath).Length
            };
        }
        catch (Exception ex)
        {
            return new BackupOperationResult
            {
                Success = false,
                Message = $"Error al restaurar base de datos: {ex.Message}",
                FilePath = sourceFilePath,
                FileSizeBytes = 0
            };
        }
    }

    private static string? FindPostgreSqlBinary(string binaryName)
    {
        string[] commonPaths =
        {
            @"C:\Program Files\PostgreSQL\16\bin",
            @"C:\Program Files\PostgreSQL\15\bin",
            @"C:\Program Files\PostgreSQL\17\bin",
            @"C:\Program Files\PostgreSQL\14\bin",
            @"C:\PostgreSQL\bin"
        };

        foreach (var path in commonPaths)
        {
            var fullPath = Path.Combine(path, binaryName);
            if (File.Exists(fullPath)) return fullPath;
        }

        // Buscar en PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var segment in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(segment.Trim(), binaryName);
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }

        return null;
    }

    private static async Task<string?> GetDockerPostgresContainerNameAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "ps --filter ancestor=postgres --format \"{{.Names}}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            string output = (await process.StandardOutput.ReadToEndAsync()).Trim();
            await process.WaitForExitAsync();

            if (!string.IsNullOrWhiteSpace(output))
            {
                return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            }

            // Fallback: listar contenedores activos y buscar coincidencias
            var psiList = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "ps --format \"{{.Names}}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var procList = Process.Start(psiList);
            if (procList == null) return null;

            string listOut = (await procList.StandardOutput.ReadToEndAsync()).Trim();
            await procList.WaitForExitAsync();

            var names = listOut.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return names.FirstOrDefault(n => n.Contains("postgres", StringComparison.OrdinalIgnoreCase) || 
                                             n.Contains("deterpos", StringComparison.OrdinalIgnoreCase) ||
                                             n.Contains("db", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    public async Task<BackupOperationResult> CreateDirectSqlDumpAsync(string targetFilePath, DatabaseConnectionParams? connectionParams = null)
    {
        var p = connectionParams ?? GetCurrentConnectionParams();
        var connStr = $"Host={p.Host};Port={p.Port};Database={p.Database};Username={p.Username};Password={p.Password};Timeout=30";
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        using var sw = new StreamWriter(targetFilePath, false, System.Text.Encoding.UTF8);
        await sw.WriteLineAsync($"-- ============================================================");
        await sw.WriteLineAsync($"-- DETERPOS ERP - COPIA DE SEGURIDAD EMPRESARIAL DIRECTA (SQL)");
        await sw.WriteLineAsync($"-- Fecha de Generación: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        await sw.WriteLineAsync($"-- Servidor: {p.Host}:{p.Port} | Base de Datos: {p.Database}");
        await sw.WriteLineAsync($"-- ============================================================");
        await sw.WriteLineAsync();
        await sw.WriteLineAsync("BEGIN;");
        await sw.WriteLineAsync("SET session_replication_role = 'replica';");
        await sw.WriteLineAsync();

        // 1. Obtener lista de tablas
        var tables = new List<string>();
        await using (var cmd = new NpgsqlCommand("SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE' ORDER BY table_name;", conn))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        long totalRowsExported = 0;

        foreach (var table in tables)
        {
            await sw.WriteLineAsync($"-- Tabla: {table}");
            await sw.WriteLineAsync($"TRUNCATE TABLE \"{table}\" CASCADE;");

            // Obtener columnas
            var columns = new List<string>();
            await using (var cmdCols = new NpgsqlCommand($"SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = '{table}' ORDER BY ordinal_position;", conn))
            await using (var readerCols = await cmdCols.ExecuteReaderAsync())
            {
                while (await readerCols.ReadAsync())
                {
                    columns.Add(readerCols.GetString(0));
                }
            }

            if (columns.Count == 0) continue;

            string colList = string.Join(", ", columns.Select(c => $"\"{c}\""));

            // Volcar filas
            await using (var cmdRows = new NpgsqlCommand($"SELECT {colList} FROM \"{table}\";", conn))
            await using (var readerRows = await cmdRows.ExecuteReaderAsync())
            {
                while (await readerRows.ReadAsync())
                {
                    var values = new List<string>();
                    for (int i = 0; i < columns.Count; i++)
                    {
                        if (readerRows.IsDBNull(i))
                        {
                            values.Add("NULL");
                        }
                        else
                        {
                            var val = readerRows.GetValue(i);
                            if (val is bool b)
                            {
                                values.Add(b ? "TRUE" : "FALSE");
                            }
                            else if (val is int || val is long || val is short || val is byte)
                            {
                                values.Add(val.ToString()!);
                            }
                            else if (val is decimal dec)
                            {
                                values.Add(dec.ToString(System.Globalization.CultureInfo.InvariantCulture));
                            }
                            else if (val is double dbl)
                            {
                                values.Add(dbl.ToString(System.Globalization.CultureInfo.InvariantCulture));
                            }
                            else if (val is float flt)
                            {
                                values.Add(flt.ToString(System.Globalization.CultureInfo.InvariantCulture));
                            }
                            else if (val is DateTime dt)
                            {
                                values.Add($"'{dt:yyyy-MM-dd HH:mm:ss.ffffffzz}'");
                            }
                            else
                            {
                                string strVal = val.ToString()!.Replace("'", "''");
                                values.Add($"'{strVal}'");
                            }
                        }
                    }

                    await sw.WriteLineAsync($"INSERT INTO \"{table}\" ({colList}) VALUES ({string.Join(", ", values)});");
                    totalRowsExported++;
                }
            }
            await sw.WriteLineAsync();
        }

        await sw.WriteLineAsync("SET session_replication_role = 'origin';");
        await sw.WriteLineAsync("COMMIT;");

        await sw.FlushAsync();
        var info = new FileInfo(targetFilePath);

        return new BackupOperationResult
        {
            Success = true,
            Message = $"Copia de seguridad SQL estructurada generada exitosamente.\nTablas: {tables.Count} | Registros: {totalRowsExported:N0}\nTamaño: {info.Length / 1024.0:N1} KB",
            FilePath = targetFilePath,
            FileSizeBytes = info.Length
        };
    }
}
