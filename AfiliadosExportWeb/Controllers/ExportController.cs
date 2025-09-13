using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using AfiliadosExportWeb.Services;
using AfiliadosExportWeb.Models;

namespace AfiliadosExportWeb.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExportController : ControllerBase
{
    private readonly IExcelExportService _excelExportService;
    private readonly IDatabaseService _databaseService;
    private readonly ILogger<ExportController> _logger;

    public ExportController(
        IExcelExportService excelExportService,
        IDatabaseService databaseService,
        ILogger<ExportController> logger)
    {
        _excelExportService = excelExportService;
        _databaseService = databaseService;
        _logger = logger;
    }

    [HttpGet("download/{fileName}")]
    public IActionResult DownloadExcel(string fileName)
    {
        try
        {
            var tempPath = Path.Combine(Directory.GetCurrentDirectory(), "TempExports", fileName);
            
            if (!System.IO.File.Exists(tempPath))
            {
                return NotFound(new { message = "El archivo no existe o ha expirado" });
            }

            var fileBytes = _excelExportService.GetExcelFile(tempPath);
            
            // Eliminar el archivo después de descargarlo
            Task.Run(() => 
            {
                Task.Delay(5000).Wait(); // Esperar 5 segundos antes de eliminar
                _excelExportService.DeleteExcelFile(tempPath);
            });

            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error descargando archivo: {fileName}");
            return StatusCode(500, new { message = "Error al descargar el archivo" });
        }
    }

    [HttpGet("databases")]
    public IActionResult GetAvailableDatabases()
    {
        try
        {
            var databases = _databaseService.GetAvailableDatabases();
            return Ok(databases);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo bases de datos disponibles");
            return StatusCode(500, new { message = "Error al obtener las bases de datos" });
        }
    }

    [HttpGet("affiliates/search")]
    public async Task<IActionResult> SearchAffiliates([FromQuery] string term, [FromQuery] string? databaseId = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
            {
                return Ok(new List<AffiliateUser>());
            }

            var affiliates = await _databaseService.SearchAffiliatesAsync(term, databaseId);
            return Ok(affiliates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error buscando afiliados con término: {term}");
            return StatusCode(500, new { message = "Error al buscar afiliados" });
        }
    }

    [HttpPost("cleanup")]
    public IActionResult CleanupTempFiles()
    {
        try
        {
            var tempPath = Path.Combine(Directory.GetCurrentDirectory(), "TempExports");
            if (Directory.Exists(tempPath))
            {
                var files = Directory.GetFiles(tempPath);
                var deletedCount = 0;
                var cutoffTime = DateTime.Now.AddHours(-1); // Eliminar archivos de más de 1 hora

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffTime)
                    {
                        try
                        {
                            System.IO.File.Delete(file);
                            deletedCount++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"No se pudo eliminar: {file}");
                        }
                    }
                }

                return Ok(new { message = $"Se eliminaron {deletedCount} archivos temporales" });
            }

            return Ok(new { message = "No hay archivos temporales para limpiar" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error limpiando archivos temporales");
            return StatusCode(500, new { message = "Error al limpiar archivos temporales" });
        }
    }
}