using Microsoft.Data.SqlClient;
using System.Data;
using Dapper;
using AfiliadosExportWeb.Models;

namespace AfiliadosExportWeb.Services;

public interface IDatabaseService
{
    Task<IEnumerable<dynamic>> GetHierarchicalPlayersAsync(string rootAffiliate, string? databaseId, IProgress<ExportProgress> progress, CancellationToken cancellationToken);
    DatabaseConfig GetDatabase(string? databaseId);
    List<DatabaseConfig> GetAvailableDatabases();
    Task<IEnumerable<AffiliateUser>> SearchAffiliatesAsync(string searchTerm, string? databaseId);
}

public class DatabaseService : IDatabaseService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseService> _logger;
    private readonly List<DatabaseConfig> _databases;

    public DatabaseService(IConfiguration configuration, ILogger<DatabaseService> logger)
    {
        _configuration = configuration;
        _logger = logger;
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

            // Ejecutar el stored procedure
            var result = await connection.QueryAsync(
                "[_V2_].[GetHierarchicalPlayersEmailVerified]",
                new { RootAffiliate = rootAffiliate },
                commandType: CommandType.StoredProcedure,
                commandTimeout: 600);

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
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Error de SQL al ejecutar stored procedure");
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
            _logger.LogError(ex, "Error al obtener datos");
            progress.Report(new ExportProgress
            {
                Status = "error",
                Message = $"Error: {ex.Message}",
                HasError = true
            });
            throw;
        }
    }

    public async Task<IEnumerable<AffiliateUser>> SearchAffiliatesAsync(string searchTerm, string? databaseId)
    {
        if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 2)
        {
            return new List<AffiliateUser>();
        }

        var database = GetDatabase(databaseId);
        var connectionString = database.GetConnectionString();

        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT TOP 20 
                    userid AS UserId, 
                    username AS Username 
                FROM _V2_Agent.HierarchicalUsers WITH(NOLOCK) 
                WHERE discriminator = 'AffiliateHierarchicalUser' 
                    AND username LIKE @searchTerm + '%'
                ORDER BY username";

            var results = await connection.QueryAsync<AffiliateUser>(sql, new { searchTerm });
            
            _logger.LogInformation($"Búsqueda de afiliados '{searchTerm}': {results.Count()} resultados");
            
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error buscando afiliados con término: {searchTerm}");
            return new List<AffiliateUser>();
        }
    }
}