using ClosedXML.Excel;
using AfiliadosExportWin.Models;
using System.Dynamic;

namespace AfiliadosExportWin.Services;

public interface IExcelExportService
{
    Task<string> GenerateExcelAsync(IEnumerable<dynamic> data, string rootAffiliate, IProgress<ExportProgress> progress, CancellationToken cancellationToken);
}

public class ExcelExportService : IExcelExportService
{
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

            // Usar la carpeta de documentos del usuario
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var exportPath = Path.Combine(documentsPath, "ExportacionesAfiliados");

            if (!Directory.Exists(exportPath))
            {
                Directory.CreateDirectory(exportPath);
            }

            var filePath = Path.Combine(exportPath, fileName);

            progress.Report(new ExportProgress
            {
                Status = "generating_excel",
                Message = "Generando archivo Excel...",
                PercentComplete = 60
            });

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Jugadores");

                // Convertir datos dinámicos a DataTable para ClosedXML
                var dataList = data.ToList();
                var totalRows = dataList.Count;

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

                    // Escribir datos con progreso
                    var rowIndex = 2;
                    var processedRows = 0;
                    var reportInterval = Math.Max(1, totalRows / 20); // Reportar cada 5%

                    foreach (var row in dataList)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        IDictionary<string, object?> rowDict;
                        if (row is IDictionary<string, object> dict2)
                        {
                            rowDict = dict2!;
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
                                    worksheet.Cell(rowIndex, col + 1).Value = dt;
                                    worksheet.Cell(rowIndex, col + 1).Style.DateFormat.Format = "yyyy-MM-dd HH:mm:ss";
                                }
                                else if (value is decimal || value is double || value is float)
                                {
                                    worksheet.Cell(rowIndex, col + 1).Value = Convert.ToDouble(value);
                                    worksheet.Cell(rowIndex, col + 1).Style.NumberFormat.Format = "#,##0.00";
                                }
                                else if (value is int || value is long)
                                {
                                    worksheet.Cell(rowIndex, col + 1).Value = Convert.ToInt64(value);
                                }
                                else if (value is bool b)
                                {
                                    worksheet.Cell(rowIndex, col + 1).Value = b ? "Sí" : "No";
                                }
                                else
                                {
                                    worksheet.Cell(rowIndex, col + 1).Value = value.ToString();
                                }
                            }
                        }

                        rowIndex++;
                        processedRows++;

                        // Reportar progreso
                        if (processedRows % reportInterval == 0 || processedRows == totalRows)
                        {
                            var percentComplete = 60 + (processedRows * 35 / totalRows); // 60% a 95%
                            progress.Report(new ExportProgress
                            {
                                Status = "writing_excel",
                                Message = $"Escribiendo datos: {processedRows:N0} / {totalRows:N0}",
                                CurrentRows = processedRows,
                                TotalRows = totalRows,
                                PercentComplete = percentComplete
                            });
                        }
                    }

                    // Ajustar ancho de columnas
                    worksheet.Columns().AdjustToContents(1, 100);

                    // Aplicar filtros automáticos
                    worksheet.RangeUsed()?.SetAutoFilter();
                }

                progress.Report(new ExportProgress
                {
                    Status = "saving_excel",
                    Message = "Guardando archivo Excel...",
                    PercentComplete = 95
                });

                await Task.Run(() => workbook.SaveAs(filePath), cancellationToken);
            }

            // Obtener tamaño del archivo
            var fileInfo = new FileInfo(filePath);
            var fileSizeMB = fileInfo.Length / (1024.0 * 1024.0);
            var elapsedTime = DateTime.Now - startTime;

            progress.Report(new ExportProgress
            {
                Status = "completed",
                Message = "Archivo Excel generado exitosamente",
                FileName = fileName,
                FilePath = filePath,
                FileSizeMB = Math.Round(fileSizeMB, 2),
                ElapsedTime = $"{elapsedTime.TotalSeconds:F1}s",
                PercentComplete = 100,
                IsComplete = true
            });

            return filePath;
        }
        catch (Exception ex)
        {
            progress.Report(new ExportProgress
            {
                Status = "error",
                Message = $"Error generando Excel: {ex.Message}",
                HasError = true
            });
            throw;
        }
    }
}