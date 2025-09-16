using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AfiliadosExportWin.Services;

namespace AfiliadosExportWin;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Configurar servicios
        var services = new ServiceCollection();
        ConfigureServices(services);

        var serviceProvider = services.BuildServiceProvider();

        // Ejecutar aplicación
        var mainForm = serviceProvider.GetRequiredService<MainForm>();
        Application.Run(mainForm);
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        // Configuración
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Servicios
        services.AddSingleton<IDatabaseService, DatabaseService>();
        services.AddSingleton<IExcelExportService, ExcelExportService>();

        // Formularios
        services.AddTransient<MainForm>();
    }
}