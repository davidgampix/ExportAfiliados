using ClosedXML.Excel;
using AfiliadosExportWeb.Models;
using System.Dynamic;

namespace AfiliadosExportWeb.Services;

public interface IExcelExportService
{
    Task<string> GenerateExcelAsync(IEnumerable<dynamic> data, string rootAffiliate, IProgress<ExportProgress> progress, CancellationToken cancellationToken);
    byte[] GetExcelFile(string filePath);
    void DeleteExcelFile(string filePath);
}

public class ExcelExportService : IExcelExportService
{
    private readonly ILogger<ExcelExportService> _logger;
    private readonly IWebHostEnvironment _environment;
    private const int MAX_ROWS_PER_SHEET = 1048575; // Excel límite: 1,048,576 (menos 1 para el header)

    public ExcelExportService(ILogger<ExcelExportService> logger, IWebHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async Task<string> GenerateExcelAsync(
        IEnumerable<dynamic> data, 
        string rootAffiliate, 
        IProgress<ExportProgress> progress,
        CancellationToken cancellationToken)
    {
        try
        {
            var startTime = DateTime.Now;
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"afiliados_export_{rootAffiliate}_{timestamp}.xlsx";
            
            // Crear directorio temporal si no existe
            var tempPath = Path.Combine(_environment.ContentRootPath, "TempExports");
            if (!Directory.Exists(tempPath))
            {
                Directory.CreateDirectory(tempPath);
            }
            
            var filePath = Path.Combine(tempPath, fileName);

            progress.Report(new ExportProgress
            {
                Status = "generating_excel",
                Message = "Generando archivo Excel...",
                PercentComplete = 60
            });

            // Convertir datos dinámicos a lista (fuera del using para reutilizar)
            var dataList = data.ToList();
            var totalRows = dataList.Count;
            var totalSheets = (int)Math.Ceiling((double)totalRows / MAX_ROWS_PER_SHEET);

            using (var workbook = new XLWorkbook())
            {
                if (totalRows > 0)
                {
                    // Obtener las columnas del primer registro
                    var firstRow = dataList[0];
                    var properties = new List<string>();

                    if (firstRow is IDictionary<string, object> dict)
                    {
                        properties = dict.Keys.ToList();
                    }
                    else if (firstRow is ExpandoObject expando)
                    {
                        properties = ((IDictionary<string, object>)expando).Keys.ToList();
                    }

                    // Información sobre hojas múltiples
                    var multipleSheets = totalSheets > 1;

                    _logger.LogInformation($"Generando {totalSheets} hoja(s) para {totalRows:N0} registros");

                    var processedRows = 0;
                    var reportInterval = Math.Max(1, totalRows / 20); // Reportar cada 5%

                    // Crear hojas según sea necesario
                    for (int sheetNum = 0; sheetNum < totalSheets; sheetNum++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var sheetName = multipleSheets ? $"Jugadores_{sheetNum + 1}" : "Jugadores";
                        var worksheet = workbook.Worksheets.Add(sheetName);

                        // Calcular rango de filas para esta hoja
                        var startRow = sheetNum * MAX_ROWS_PER_SHEET;
                        var endRow = Math.Min(startRow + MAX_ROWS_PER_SHEET, totalRows);
                        var rowsInSheet = endRow - startRow;

                        progress.Report(new ExportProgress
                        {
                            Status = "writing_excel",
                            Message = multipleSheets
                                ? $"Creando hoja {sheetNum + 1} de {totalSheets}..."
                                : "Escribiendo datos...",
                            CurrentRows = processedRows,
                            TotalRows = totalRows,
                            PercentComplete = 60 + (processedRows * 35 / totalRows)
                        });

                        // Escribir encabezados
                        for (int col = 0; col < properties.Count; col++)
                        {
                            worksheet.Cell(1, col + 1).Value = properties[col];
                        }

                        // Aplicar formato a encabezados
                        var headerRange = worksheet.Range(1, 1, 1, properties.Count);
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.LightGreen;
                        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        // Escribir datos de esta hoja
                        var excelRowIndex = 2;
                        for (int dataIndex = startRow; dataIndex < endRow; dataIndex++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var row = dataList[dataIndex];

                            IDictionary<string, object?> rowDict;
                            if (row is IDictionary<string, object> dict2)
                            {
                                rowDict = dict2;
                            }
                            else if (row is ExpandoObject expando2)
                            {
                                rowDict = (IDictionary<string, object?>)expando2;
                            }
                            else
                            {
                                continue;
                            }

                            for (int col = 0; col < properties.Count; col++)
                            {
                                var value = rowDict.ContainsKey(properties[col]) ? rowDict[properties[col]] : null;

                                if (value != null)
                                {
                                    if (value is DateTime dt)
                                    {
                                        worksheet.Cell(excelRowIndex, col + 1).Value = dt;
                                        worksheet.Cell(excelRowIndex, col + 1).Style.DateFormat.Format = "yyyy-MM-dd HH:mm:ss";
                                    }
                                    else if (value is decimal || value is double || value is float)
                                    {
                                        worksheet.Cell(excelRowIndex, col + 1).Value = Convert.ToDouble(value);
                                        worksheet.Cell(excelRowIndex, col + 1).Style.NumberFormat.Format = "#,##0.00";
                                    }
                                    else if (value is int || value is long)
                                    {
                                        worksheet.Cell(excelRowIndex, col + 1).Value = Convert.ToInt64(value);
                                    }
                                    else if (value is bool b)
                                    {
                                        worksheet.Cell(excelRowIndex, col + 1).Value = b ? "Sí" : "No";
                                    }
                                    else
                                    {
                                        worksheet.Cell(excelRowIndex, col + 1).Value = value.ToString();
                                    }
                                }
                            }

                            excelRowIndex++;
                            processedRows++;

                            // Reportar progreso
                            if (processedRows % reportInterval == 0 || processedRows == totalRows)
                            {
                                var percentComplete = 60 + (processedRows * 35 / totalRows); // 60% a 95%
                                progress.Report(new ExportProgress
                                {
                                    Status = "writing_excel",
                                    Message = multipleSheets
                                        ? $"Hoja {sheetNum + 1}/{totalSheets}: {processedRows:N0} / {totalRows:N0} registros"
                                        : $"Escribiendo datos: {processedRows:N0} / {totalRows:N0}",
                                    CurrentRows = processedRows,
                                    TotalRows = totalRows,
                                    PercentComplete = percentComplete
                                });
                            }
                        }

                        // Ajustar ancho de columnas
                        worksheet.Columns().AdjustToContents(1, 100);

                        // Aplicar filtros automáticos
                        worksheet.RangeUsed().SetAutoFilter();

                        _logger.LogInformation($"Hoja '{sheetName}' completada con {rowsInSheet:N0} registros");
                    }
                }

                progress.Report(new ExportProgress
                {
                    Status = "saving_excel",
                    Message = totalRows > MAX_ROWS_PER_SHEET
                        ? $"Guardando archivo con {totalRows:N0} registros en múltiples hojas..."
                        : "Guardando archivo Excel...",
                    PercentComplete = 95
                });

                await Task.Run(() => workbook.SaveAs(filePath), cancellationToken);
            }

            // Obtener tamaño del archivo
            var fileInfo = new FileInfo(filePath);
            var fileSizeMB = fileInfo.Length / (1024.0 * 1024.0);
            var elapsedTime = DateTime.Now - startTime;

            // Mensaje de finalización
            var completionMessage = totalSheets > 1
                ? $"Excel generado con {totalRows:N0} registros en {totalSheets} hojas"
                : "Archivo Excel generado exitosamente";

            progress.Report(new ExportProgress
            {
                Status = "completed",
                Message = completionMessage,
                FileName = fileName,
                FilePath = filePath,
                FileSizeMB = Math.Round(fileSizeMB, 2),
                ElapsedTime = $"{elapsedTime.TotalSeconds:F1}s",
                PercentComplete = 100,
                IsComplete = true
            });

            _logger.LogInformation($"Excel generado: {fileName} ({fileSizeMB:F2} MB) - {totalSheets} hoja(s) - en {elapsedTime.TotalSeconds:F1}s");

            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando Excel");
            progress.Report(new ExportProgress
            {
                Status = "error",
                Message = $"Error generando Excel: {ex.Message}",
                HasError = true
            });
            throw;
        }
    }

    public byte[] GetExcelFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("El archivo Excel no existe", filePath);
        }

        return File.ReadAllBytes(filePath);
    }

    public void DeleteExcelFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation($"Archivo temporal eliminado: {filePath}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"No se pudo eliminar el archivo temporal: {filePath}");
        }
    }
}