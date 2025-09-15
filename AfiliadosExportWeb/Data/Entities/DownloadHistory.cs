using System.ComponentModel.DataAnnotations;

namespace AfiliadosExportWeb.Data.Entities;

public class DownloadHistory
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string AffiliateCode { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty; // Usuario que realizó la descarga
    
    public int OperationId { get; set; }
    
    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;
    
    public long FileSizeBytes { get; set; }
    
    public int RecordCount { get; set; }
    
    public DateTime DownloadedAt { get; set; } = DateTime.UtcNow;
    
    public TimeSpan ProcessingTime { get; set; }
    
    public bool IsDeleted { get; set; } = false; // Borrado lógico
    
    public DateTime? DeletedAt { get; set; }
    
    [MaxLength(100)]
    public string? DeletedBy { get; set; }
    
    // Navigation property
    public virtual Operation Operation { get; set; } = null!;
}