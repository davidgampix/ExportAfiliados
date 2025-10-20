using Microsoft.AspNetCore.SignalR;
using AfiliadosExportWeb.Models;
using AfiliadosExportWeb.Services;
using System.Collections.Concurrent;

namespace AfiliadosExportWeb.Hubs;

public class ExportHub : Hub
{
    private readonly IDatabaseService _databaseService;
    private readonly IExcelExportService _excelExportService;
    private readonly ILogger<ExportHub> _logger;

    // Diccionario para mantener los CancellationTokenSource activos por conexión
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> _activeCancellations = new();

    // Mensajes aleatorios para mostrar durante la ejecución larga
    private static readonly string[] _workingMessages = new[]
    {
        "Procesando datos jerárquicos...",
        "Analizando estructura de afiliados...",
        "Recopilando información de jugadores...",
        "Verificando emails de usuarios...",
        "Construyendo árbol de afiliación...",
        "Extrayendo registros relacionados...",
        "Calculando jerarquías...",
        "Consolidando información...",
        "Optimizando resultados...",
        "Preparando datos para exportación...",
        "Esto puede tomar varios minutos...",
        "Procesando gran cantidad de registros...",
        "El sistema está trabajando correctamente...",
        "Por favor, sea paciente...",
        "Obteniendo datos del servidor..."
    };

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

        // Crear un CancellationTokenSource para esta exportación
        var cts = new CancellationTokenSource();
        _activeCancellations[connectionId] = cts;

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

            // Iniciar cronómetro en segundo plano
            var stopwatchTask = StartStopwatchAsync(connectionId, cts.Token);

            // Obtener datos de la base de datos
            var data = await _databaseService.GetHierarchicalPlayersAsync(
                request.RootAffiliate,
                request.DatabaseId,
                progress,
                cts.Token);

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
                cts.Token);

            _logger.LogInformation($"Exportación completada: {filePath}");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation($"Exportación cancelada por el usuario para {request.RootAffiliate}");
            await Clients.Caller.SendAsync("ExportProgress", new ExportProgress
            {
                Status = "cancelled",
                Message = "Exportación cancelada por el usuario",
                HasError = true
            });
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
        finally
        {
            // Limpiar el CancellationTokenSource
            _activeCancellations.TryRemove(connectionId, out _);
            cts?.Dispose();
        }
    }

    public async Task CancelExport()
    {
        var connectionId = Context.ConnectionId;
        _logger.LogInformation($"Solicitud de cancelación recibida - ConnectionId: {connectionId}");

        if (_activeCancellations.TryGetValue(connectionId, out var cts))
        {
            cts.Cancel();
            _logger.LogInformation($"Exportación cancelada - ConnectionId: {connectionId}");
            await Clients.Caller.SendAsync("ExportCancelled");
        }
    }

    private async Task StartStopwatchAsync(string connectionId, CancellationToken cancellationToken)
    {
        var startTime = DateTime.Now;
        var random = new Random();
        var lastMessageIndex = -1;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(5000, cancellationToken); // Actualizar cada 5 segundos

                var elapsed = DateTime.Now - startTime;
                var elapsedSeconds = (int)elapsed.TotalSeconds;

                // Seleccionar un mensaje aleatorio diferente al anterior
                int messageIndex;
                do
                {
                    messageIndex = random.Next(_workingMessages.Length);
                } while (messageIndex == lastMessageIndex && _workingMessages.Length > 1);

                lastMessageIndex = messageIndex;

                await Clients.Client(connectionId).SendAsync("TimerUpdate", new
                {
                    ElapsedSeconds = elapsedSeconds,
                    ElapsedFormatted = FormatElapsedTime(elapsed),
                    RandomMessage = _workingMessages[messageIndex]
                }, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cuando se cancela
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en el cronómetro");
        }
    }

    private static string FormatElapsedTime(TimeSpan elapsed)
    {
        if (elapsed.TotalMinutes >= 1)
        {
            return $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2} min";
        }
        return $"{elapsed.Seconds} seg";
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation($"Cliente conectado: {Context.ConnectionId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        _logger.LogInformation($"Cliente desconectado: {connectionId}");

        // Cancelar cualquier exportación activa al desconectarse
        if (_activeCancellations.TryRemove(connectionId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        await base.OnDisconnectedAsync(exception);
    }
}