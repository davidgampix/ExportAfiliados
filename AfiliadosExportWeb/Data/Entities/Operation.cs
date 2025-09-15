using System.ComponentModel.DataAnnotations;

namespace AfiliadosExportWeb.Data.Entities;

public class Operation
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty; // sportsbet, cdl, formowin, etc
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty; // SportsBet, CDL, FormoWin, etc
    
    [Required]
    [MaxLength(500)]
    public string ConnectionString { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string Server { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string Database { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
    
    public bool IsDefault { get; set; } = false;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation property
    public virtual ICollection<DownloadHistory> Downloads { get; set; } = new List<DownloadHistory>();
}