using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using AfiliadosExportWeb.Services;
using AfiliadosExportWeb.Data.Entities;

namespace AfiliadosExportWeb.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly IOperationService _operationService;
    private readonly IHistoryService _historyService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IOperationService operationService,
        IHistoryService historyService,
        ILogger<AdminController> logger)
    {
        _operationService = operationService;
        _historyService = historyService;
        _logger = logger;
    }

    // Operations Management
    [HttpGet("operations")]
    public async Task<IActionResult> GetOperations()
    {
        try
        {
            var operations = await _operationService.GetAllOperationsAsync();
            return Ok(operations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo operaciones");
            return StatusCode(500, new { message = "Error al obtener operaciones" });
        }
    }

    [HttpGet("operations/{id}")]
    public async Task<IActionResult> GetOperation(int id)
    {
        try
        {
            var operation = await _operationService.GetOperationByIdAsync(id);
            if (operation == null)
                return NotFound(new { message = "Operación no encontrada" });
            
            return Ok(operation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error obteniendo operación {id}");
            return StatusCode(500, new { message = "Error al obtener la operación" });
        }
    }

    [HttpPost("operations")]
    public async Task<IActionResult> CreateOperation([FromBody] Operation operation)
    {
        try
        {
            var created = await _operationService.CreateOperationAsync(operation);
            return CreatedAtAction(nameof(GetOperation), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando operación");
            return StatusCode(500, new { message = "Error al crear la operación" });
        }
    }

    [HttpPut("operations/{id}")]
    public async Task<IActionResult> UpdateOperation(int id, [FromBody] Operation operation)
    {
        try
        {
            if (id != operation.Id)
                return BadRequest(new { message = "ID no coincide" });

            var updated = await _operationService.UpdateOperationAsync(operation);
            return Ok(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error actualizando operación {id}");
            return StatusCode(500, new { message = "Error al actualizar la operación" });
        }
    }

    [HttpDelete("operations/{id}")]
    public async Task<IActionResult> DeleteOperation(int id)
    {
        try
        {
            var result = await _operationService.DeleteOperationAsync(id);
            if (!result)
                return NotFound(new { message = "Operación no encontrada" });
            
            return Ok(new { message = "Operación desactivada correctamente" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error eliminando operación {id}");
            return StatusCode(500, new { message = "Error al eliminar la operación" });
        }
    }

    // History Management
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int? operationId, [FromQuery] string? username)
    {
        try
        {
            var history = await _historyService.GetHistoryAsync(operationId, username);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo historial");
            return StatusCode(500, new { message = "Error al obtener el historial" });
        }
    }

    [HttpGet("history/{id}")]
    public async Task<IActionResult> GetHistoryById(int id)
    {
        try
        {
            var history = await _historyService.GetHistoryByIdAsync(id);
            if (history == null)
                return NotFound(new { message = "Registro no encontrado" });
            
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error obteniendo historial {id}");
            return StatusCode(500, new { message = "Error al obtener el registro" });
        }
    }

    [HttpDelete("history/{id}")]
    public async Task<IActionResult> DeleteHistory(int id, [FromQuery] bool physical = false)
    {
        try
        {
            var username = User.Identity?.Name ?? "System";
            var result = await _historyService.DeleteHistoryAsync(id, username, physical);
            
            if (!result)
                return NotFound(new { message = "Registro no encontrado" });
            
            return Ok(new { 
                message = physical ? "Registro eliminado físicamente" : "Registro marcado como eliminado" 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error eliminando historial {id}");
            return StatusCode(500, new { message = "Error al eliminar el registro" });
        }
    }

    [HttpDelete("history")]
    public async Task<IActionResult> DeleteAllHistory([FromQuery] bool physical = false)
    {
        try
        {
            var username = User.Identity?.Name ?? "System";
            await _historyService.DeleteAllHistoryAsync(username, physical);
            
            return Ok(new { 
                message = physical ? "Todos los registros eliminados físicamente" : "Todos los registros marcados como eliminados" 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error eliminando todo el historial");
            return StatusCode(500, new { message = "Error al eliminar el historial" });
        }
    }

    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics()
    {
        try
        {
            var stats = await _historyService.GetStatisticsAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo estadísticas");
            return StatusCode(500, new { message = "Error al obtener estadísticas" });
        }
    }
}