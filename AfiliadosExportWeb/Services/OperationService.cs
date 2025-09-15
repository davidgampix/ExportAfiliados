using Microsoft.EntityFrameworkCore;
using AfiliadosExportWeb.Data;
using AfiliadosExportWeb.Data.Entities;

namespace AfiliadosExportWeb.Services;

public interface IOperationService
{
    Task<List<Operation>> GetAllOperationsAsync();
    Task<Operation?> GetOperationByIdAsync(int id);
    Task<Operation?> GetOperationByCodeAsync(string code);
    Task<Operation> CreateOperationAsync(Operation operation);
    Task<Operation> UpdateOperationAsync(Operation operation);
    Task<bool> DeleteOperationAsync(int id);
    Task<Operation?> GetDefaultOperationAsync();
}

public class OperationService : IOperationService
{
    private readonly AppDbContext _context;
    private readonly ILogger<OperationService> _logger;

    public OperationService(AppDbContext context, ILogger<OperationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<Operation>> GetAllOperationsAsync()
    {
        return await _context.Operations
            .Where(o => o.IsActive)
            .OrderBy(o => o.Name)
            .ToListAsync();
    }

    public async Task<Operation?> GetOperationByIdAsync(int id)
    {
        return await _context.Operations
            .FirstOrDefaultAsync(o => o.Id == id && o.IsActive);
    }

    public async Task<Operation?> GetOperationByCodeAsync(string code)
    {
        return await _context.Operations
            .FirstOrDefaultAsync(o => o.Code == code && o.IsActive);
    }

    public async Task<Operation> CreateOperationAsync(Operation operation)
    {
        operation.CreatedAt = DateTime.UtcNow;
        _context.Operations.Add(operation);
        await _context.SaveChangesAsync();
        _logger.LogInformation($"Operación creada: {operation.Name} ({operation.Code})");
        return operation;
    }

    public async Task<Operation> UpdateOperationAsync(Operation operation)
    {
        operation.UpdatedAt = DateTime.UtcNow;
        _context.Operations.Update(operation);
        await _context.SaveChangesAsync();
        _logger.LogInformation($"Operación actualizada: {operation.Name} ({operation.Code})");
        return operation;
    }

    public async Task<bool> DeleteOperationAsync(int id)
    {
        var operation = await _context.Operations.FindAsync(id);
        if (operation == null)
            return false;

        operation.IsActive = false;
        operation.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _logger.LogInformation($"Operación desactivada: {operation.Name} ({operation.Code})");
        return true;
    }

    public async Task<Operation?> GetDefaultOperationAsync()
    {
        return await _context.Operations
            .FirstOrDefaultAsync(o => o.IsDefault && o.IsActive);
    }
}