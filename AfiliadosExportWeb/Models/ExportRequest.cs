namespace AfiliadosExportWeb.Models;

public class ExportRequest
{
    public string RootAffiliate { get; set; } = string.Empty;
    public string? DatabaseId { get; set; }
    public string? StatusIds { get; set; }  // Lista de IDs separados por coma (0,1,2)
    public int SelfExclusionFilter { get; set; } = 0;  // 0 = sin autoexcluidos, 1 = solo autoexcluidos, 2 = todos
}

public class ExportProgress
{
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int CurrentRows { get; set; }
    public int TotalRows { get; set; }
    public double PercentComplete { get; set; }
    public string? FileName { get; set; }
    public string? FilePath { get; set; }
    public double? FileSizeMB { get; set; }
    public string? ElapsedTime { get; set; }
    public bool IsComplete { get; set; }
    public bool HasError { get; set; }
}