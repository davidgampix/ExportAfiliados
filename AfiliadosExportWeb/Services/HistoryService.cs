using Microsoft.EntityFrameworkCore;
using AfiliadosExportWeb.Data;
using AfiliadosExportWeb.Data.Entities;

namespace AfiliadosExportWeb.Services;

public interface IHistoryService
{
    Task<DownloadHistory> AddDownloadHistoryAsync(string affiliateCode, string username, int operationId, 
        string fileName, string filePath, long fileSizeBytes, int recordCount, TimeSpan processingTime);
    Task<List<DownloadHistory>> GetHistoryAsync(int? operationId = null, string? username = null);
    Task<DownloadHistory?> GetHistoryByIdAsync(int id);
    Task<bool> DeleteHistoryAsync(int id, string deletedBy, bool physicalDelete = false);
    Task<bool> DeleteAllHistoryAsync(string deletedBy, bool physicalDelete = false);
    Task<Dictionary<string, object>> GetStatisticsAsync();
}

public class HistoryService : IHistoryService
{
    private readonly AppDbContext _context;
    private readonly ILogger<HistoryService> _logger;
    private readonly IExcelExportService _excelExportService;

    public HistoryService(AppDbContext context, ILogger<HistoryService> logger, IExcelExportService excelExportService)
    {
        _context = context;
        _logger = logger;
        _excelExportService = excelExportService;
    }

    public async Task<DownloadHistory> AddDownloadHistoryAsync(
        string affiliateCode, 
        string username, 
        int operationId,
        string fileName, 
        string filePath, 
        long fileSizeBytes, 
        int recordCount, 
        TimeSpan processingTime)
    {
        var history = new DownloadHistory
        {
            AffiliateCode = affiliateCode,
            Username = username,
            OperationId = operationId,
            FileName = fileName,
            FilePath = filePath,
            FileSizeBytes = fileSizeBytes,
            RecordCount = recordCount,
            ProcessingTime = processingTime,
            DownloadedAt = DateTime.UtcNow
        };

        _context.DownloadHistories.Add(history);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation($"Historial registrado: {affiliateCode} por {username}");
        return history;
    }

    public async Task<List<DownloadHistory>> GetHistoryAsync(int? operationId = null, string? username = null)
    {
        var query = _context.DownloadHistories
            .Include(h => h.Operation)
            .Where(h => !h.IsDeleted);

        if (operationId.HasValue)
            query = query.Where(h => h.OperationId == operationId.Value);

        if (!string.IsNullOrEmpty(username))
            query = query.Where(h => h.Username == username);

        return await query
            .OrderByDescending(h => h.DownloadedAt)
            .Take(100) // Limitar a los últimos 100 registros
            .ToListAsync();
    }

    public async Task<DownloadHistory?> GetHistoryByIdAsync(int id)
    {
        return await _context.DownloadHistories
            .Include(h => h.Operation)
            .FirstOrDefaultAsync(h => h.Id == id && !h.IsDeleted);
    }

    public async Task<bool> DeleteHistoryAsync(int id, string deletedBy, bool physicalDelete = false)
    {
        var history = await _context.DownloadHistories.FindAsync(id);
        if (history == null)
            return false;

        if (physicalDelete)
        {
            // Eliminar archivo físico si existe
            if (!string.IsNullOrEmpty(history.FilePath) && File.Exists(history.FilePath))
            {
                try
                {
                    _excelExportService.DeleteExcelFile(history.FilePath);
                    _logger.LogInformation($"Archivo físico eliminado: {history.FilePath}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"No se pudo eliminar el archivo: {history.FilePath}");
                }
            }

            // Eliminar registro de la base de datos
            _context.DownloadHistories.Remove(history);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Historial eliminado físicamente: ID {id}");
        }
        else
        {
            // Borrado lógico
            history.IsDeleted = true;
            history.DeletedAt = DateTime.UtcNow;
            history.DeletedBy = deletedBy;
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Historial marcado como eliminado: ID {id}");
        }

        return true;
    }

    public async Task<bool> DeleteAllHistoryAsync(string deletedBy, bool physicalDelete = false)
    {
        var histories = await _context.DownloadHistories
            .Where(h => !h.IsDeleted)
            .ToListAsync();

        if (physicalDelete)
        {
            // Eliminar archivos físicos
            foreach (var history in histories)
            {
                if (!string.IsNullOrEmpty(history.FilePath) && File.Exists(history.FilePath))
                {
                    try
                    {
                        _excelExportService.DeleteExcelFile(history.FilePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"No se pudo eliminar el archivo: {history.FilePath}");
                    }
                }
            }

            // Eliminar todos los registros
            _context.DownloadHistories.RemoveRange(histories);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Todos los historiales eliminados físicamente por {deletedBy}");
        }
        else
        {
            // Borrado lógico masivo
            foreach (var history in histories)
            {
                history.IsDeleted = true;
                history.DeletedAt = DateTime.UtcNow;
                history.DeletedBy = deletedBy;
            }
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Todos los historiales marcados como eliminados por {deletedBy}");
        }

        return true;
    }

    public async Task<Dictionary<string, object>> GetStatisticsAsync()
    {
        var stats = new Dictionary<string, object>();

        var histories = await _context.DownloadHistories
            .Where(h => !h.IsDeleted)
            .ToListAsync();

        stats["totalDownloads"] = histories.Count;
        stats["totalSizeGB"] = Math.Round(histories.Sum(h => h.FileSizeBytes) / (1024.0 * 1024.0 * 1024.0), 2);
        stats["totalRecords"] = histories.Sum(h => h.RecordCount);
        stats["averageProcessingTimeSeconds"] = histories.Any() ? 
            Math.Round(histories.Average(h => h.ProcessingTime.TotalSeconds), 2) : 0;

        // Top afiliados
        var topAffiliates = histories
            .GroupBy(h => h.AffiliateCode)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new { affiliate = g.Key, count = g.Count() })
            .ToList();
        stats["topAffiliates"] = topAffiliates;

        // Descargas por operación
        var downloadsByOperation = await _context.DownloadHistories
            .Include(h => h.Operation)
            .Where(h => !h.IsDeleted)
            .GroupBy(h => h.Operation.Name)
            .Select(g => new { operation = g.Key, count = g.Count() })
            .ToListAsync();
        stats["downloadsByOperation"] = downloadsByOperation;

        return stats;
    }
}