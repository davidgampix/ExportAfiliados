using AfiliadosExportWeb.Hubs;
using AfiliadosExportWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS for SignalR
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.SetIsOriginAllowed(origin => true)
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
    });
});

// Register custom services
builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
builder.Services.AddSingleton<IExcelExportService, ExcelExportService>();

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

// Serve static files (HTML, CSS, JS)
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

app.MapControllers();
app.MapHub<ExportHub>("/exportHub");

// Fallback to index.html for SPA
app.MapFallbackToFile("index.html");

// Clean up temp files on startup
var excelService = app.Services.GetRequiredService<IExcelExportService>();
var tempPath = Path.Combine(Directory.GetCurrentDirectory(), "TempExports");
if (Directory.Exists(tempPath))
{
    var files = Directory.GetFiles(tempPath);
    foreach (var file in files)
    {
        try
        {
            File.Delete(file);
        }
        catch { }
    }
}

app.Run();