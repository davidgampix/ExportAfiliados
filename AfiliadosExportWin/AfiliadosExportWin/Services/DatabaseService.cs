using Microsoft.Data.SqlClient;
using System.Data;
using Dapper;
using AfiliadosExportWin.Models;
using Microsoft.Extensions.Configuration;

namespace AfiliadosExportWin.Services;

public interface IDatabaseService
{
    Task<IEnumerable<dynamic>> GetHierarchicalPlayersAsync(string rootAffiliate, string? databaseId, IProgress<ExportProgress> progress, CancellationToken cancellationToken);
    DatabaseConfig GetDatabase(string? databaseId);
    List<DatabaseConfig> GetAvailableDatabases();
}

public class DatabaseService : IDatabaseService
{
    private readonly IConfiguration _configuration;
    private readonly List<DatabaseConfig> _databases;

    public DatabaseService(IConfiguration configuration)
    {
        _configuration = configuration;
        _databases = configuration.GetSection("DatabaseSettings:Databases").Get<List<DatabaseConfig>>() ?? new List<DatabaseConfig>();

        // Si no hay configuración, usar la base de datos por defecto
        if (!_databases.Any())
        {
            _databases.Add(new DatabaseConfig
            {
                Id = "sportsbet",
                Name = "SportsBet Afiliados",
                Server = "54.226.82.137",
                Database = "SportsBet_Afiliados",
                Username = "SportsBetLogin",
                Password = "8B24BDF8-9541-47F8-9957-B63DD87FEFCE",
                IsDefault = true
            });
        }
    }

    public DatabaseConfig GetDatabase(string? databaseId)
    {
        if (string.IsNullOrEmpty(databaseId))
        {
            return _databases.FirstOrDefault(d => d.IsDefault) ?? _databases.First();
        }

        return _databases.FirstOrDefault(d => d.Id == databaseId) ?? _databases.First();
    }

    public List<DatabaseConfig> GetAvailableDatabases()
    {
        return _databases.Select(d => new DatabaseConfig
        {
            Id = d.Id,
            Name = d.Name,
            IsDefault = d.IsDefault
        }).ToList();
    }

    public async Task<IEnumerable<dynamic>> GetHierarchicalPlayersAsync(
        string rootAffiliate,
        string? databaseId,
        IProgress<ExportProgress> progress,
        CancellationToken cancellationToken)
    {
        var database = GetDatabase(databaseId);
        var connectionString = database.GetConnectionString();

        try
        {
            progress.Report(new ExportProgress
            {
                Status = "connecting",
                Message = $"Conectando a {database.Name}...",
                PercentComplete = 0
            });

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            progress.Report(new ExportProgress
            {
                Status = "executing",
                Message = "Ejecutando stored procedure...",
                PercentComplete = 10
            });

            var startTime = DateTime.Now;

            // Crear tarea para animar el progreso mientras ejecuta el stored procedure
            var progressAnimationCts = new CancellationTokenSource();
            var progressAnimation = Task.Run(async () =>
            {
                int currentProgress = 10;
                int animationSpeed = 500; // Velocidad inicial en ms

                while (!progressAnimationCts.Token.IsCancellationRequested && currentProgress < 48)
                {
                    await Task.Delay(animationSpeed, progressAnimationCts.Token);

                    // Hacer la animación más lenta conforme pasa el tiempo
                    if (currentProgress > 20) animationSpeed = 700;
                    if (currentProgress > 30) animationSpeed = 1000;
                    if (currentProgress > 40) animationSpeed = 1500;

                    currentProgress++;

                    var elapsed = (DateTime.Now - startTime).TotalSeconds;
                    var message = elapsed > 30
                        ? $"Procesando gran cantidad de datos... ({elapsed:F0}s)"
                        : $"Ejecutando stored procedure... ({elapsed:F0}s)";

                    progress.Report(new ExportProgress
                    {
                        Status = "executing",
                        Message = message,
                        PercentComplete = currentProgress
                    });
                }

                // Si llegamos al límite, mantener en 48% con mensaje de espera
                while (!progressAnimationCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(2000, progressAnimationCts.Token);
                    var elapsed = (DateTime.Now - startTime).TotalSeconds;
                    progress.Report(new ExportProgress
                    {
                        Status = "executing",
                        Message = $"Procesando gran cantidad de datos, por favor espere... ({elapsed:F0}s)",
                        PercentComplete = 48
                    });
                }
            }, progressAnimationCts.Token);

            try
            {
                // Ejecutar el stored procedure con timeout extendido (20 minutos)
                var result = await connection.QueryAsync(
                    "[_V2_].[GetHierarchicalPlayersEmailVerified]",
                    new { RootAffiliate = rootAffiliate },
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 1200); // 20 minutos de timeout

                // Detener la animación de progreso
                progressAnimationCts.Cancel();
                try { await progressAnimation; } catch { }

                return await ProcessResults(result, progress, startTime);
            }
            finally
            {
                progressAnimationCts.Cancel();
                progressAnimationCts.Dispose();
            }
        }
        catch (SqlException ex)
        {
            progress.Report(new ExportProgress
            {
                Status = "error",
                Message = $"Error de base de datos: {ex.Message}",
                HasError = true
            });
            throw;
        }
        catch (Exception ex)
        {
            progress.Report(new ExportProgress
            {
                Status = "error",
                Message = $"Error: {ex.Message}",
                HasError = true
            });
            throw;
        }
    }

    private async Task<IEnumerable<dynamic>> ProcessResults(IEnumerable<dynamic> result, IProgress<ExportProgress> progress, DateTime startTime)
    {
        await Task.Yield(); // Asegurar que es async

        var dataList = result.ToList();
        var totalRows = dataList.Count;

        var elapsedTime = DateTime.Now - startTime;

        progress.Report(new ExportProgress
        {
            Status = "data_loaded",
            Message = $"Datos cargados: {totalRows:N0} registros",
            CurrentRows = totalRows,
            TotalRows = totalRows,
            PercentComplete = 50,
            ElapsedTime = $"{elapsedTime.TotalSeconds:F1}s"
        });

        return dataList;
    }
}