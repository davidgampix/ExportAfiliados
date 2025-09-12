namespace AfiliadosExportWeb.Models;

public class PlayerData
{
    public int Id { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public string? Country { get; set; }
    public string? Currency { get; set; }
    public string? AffiliateCode { get; set; }
    public string? ParentAffiliate { get; set; }
    public DateTime? RegistrationDate { get; set; }
    public DateTime? LastLogin { get; set; }
    public bool EmailVerified { get; set; }
    public bool IsActive { get; set; }
    public decimal? Balance { get; set; }
    public decimal? TotalDeposits { get; set; }
    public decimal? TotalWithdrawals { get; set; }
    public int? Level { get; set; }
    public string? Status { get; set; }
    
    // Propiedades adicionales dinámicas del SP
    public Dictionary<string, object?> AdditionalProperties { get; set; } = new();
}