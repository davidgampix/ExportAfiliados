namespace AfiliadosExportWin.Models;

public class DatabaseConfig
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool IsDefault { get; set; }

    public string GetConnectionString()
    {
        return $"Server={Server};Database={Database};User Id={Username};Password={Password};TrustServerCertificate=true;Connection Timeout=60;Command Timeout=1200;";
    }
}

public class DatabaseSettings
{
    public List<DatabaseConfig> Databases { get; set; } = new();
}