using Microsoft.AspNetCore.SignalR;
using AfiliadosExportWeb.Models;
using AfiliadosExportWeb.Services;

namespace AfiliadosExportWeb.Hubs;

public class ExportHub : Hub
{
    private readonly IDatabaseService _databaseService;
    private readonly IExcelExportService _excelExportService;
    private readonly ILogger<ExportHub> _logger;

    public ExportHub(
        IDatabaseService databaseService,
        IExcelExportService excelExportService,
        ILogger<ExportHub> logger)
    {
        _databaseService = databaseService;
        _excelExportService = excelExportService;
        _logger = logger;
    }

    public async Task StartExport(ExportRequest request)
    {
        var connectionId = Context.ConnectionId;
        _logger.LogInformation($"Iniciando exportación para {request.RootAffiliate} - ConnectionId: {connectionId}");

        try
        {
            // Validación
            if (string.IsNullOrWhiteSpace(request.RootAffiliate))
            {
                await Clients.Caller.SendAsync("ExportProgress", new ExportProgress
                {
                    Status = "error",
                    Message = "Debe ingresar un nombre de afiliado",
                    HasError = true
                });
                return;
            }

            // Progress reporter para SignalR
            var progress = new Progress<ExportProgress>(async update =>
            {
                await Clients.Caller.SendAsync("ExportProgress", update);
            });

            // Obtener datos de la base de datos
            var cancellationToken = CancellationToken.None;
            var data = await _databaseService.GetHierarchicalPlayersAsync(
                request.RootAffiliate,
                request.DatabaseId,
                progress,
                cancellationToken);

            if (!data.Any())
            {
                await Clients.Caller.SendAsync("ExportProgress", new ExportProgress
                {
                    Status = "error",
                    Message = "No se encontraron datos para el afiliado especificado",
                    HasError = true
                });
                return;
            }

            // Generar Excel
            var filePath = await _excelExportService.GenerateExcelAsync(
                data,
                request.RootAffiliate,
                progress,
                cancellationToken);

            _logger.LogInformation($"Exportación completada: {filePath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error en exportación para {request.RootAffiliate}");
            await Clients.Caller.SendAsync("ExportProgress", new ExportProgress
            {
                Status = "error",
                Message = $"Error: {ex.Message}",
                HasError = true
            });
        }
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation($"Cliente conectado: {Context.ConnectionId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation($"Cliente desconectado: {Context.ConnectionId}");
        await base.OnDisconnectedAsync(exception);
    }
}