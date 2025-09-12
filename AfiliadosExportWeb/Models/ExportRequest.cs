namespace AfiliadosExportWeb.Models;

public class ExportRequest
{
    public string RootAffiliate { get; set; } = string.Empty;
    public string? DatabaseId { get; set; }
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