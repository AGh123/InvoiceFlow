using InvoiceFlow.Application.Invoices.Abstractions;
using InvoiceFlow.Infrastructure;
using InvoiceFlow.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.IntegrationTests.Persistence;

internal sealed class TestDatabase : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly string _directoryPath;

    private TestDatabase(ServiceProvider services, string directoryPath, string databasePath)
    {
        _services = services;
        _directoryPath = directoryPath;
        DatabasePath = databasePath;
    }

    public string DatabasePath { get; }

    public IInvoiceRepository Repository =>
        _services.GetRequiredService<IInvoiceRepository>();

    public IDbContextFactory<InvoiceFlowDbContext> DbContextFactory =>
        _services.GetRequiredService<IDbContextFactory<InvoiceFlowDbContext>>();

    public static async Task<TestDatabase> CreateAsync(string? targetMigration = null)
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "InvoiceFlow.IntegrationTests",
            Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directoryPath, "invoiceflow.db");

        var services = new ServiceCollection();
        services.AddInfrastructure(databasePath);
        var serviceProvider = services.BuildServiceProvider();

        try
        {
            if (targetMigration is null)
            {
                await serviceProvider.ApplyDatabaseMigrationsAsync();
            }
            else
            {
                await using var dbContext = await serviceProvider
                    .GetRequiredService<IDbContextFactory<InvoiceFlowDbContext>>()
                    .CreateDbContextAsync();
                await dbContext.Database.MigrateAsync(targetMigration);
            }
            return new TestDatabase(serviceProvider, directoryPath, databasePath);
        }
        catch
        {
            await serviceProvider.DisposeAsync();
            SqliteConnection.ClearAllPools();
            DeleteDatabaseDirectory(directoryPath);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        SqliteConnection.ClearAllPools();
        DeleteDatabaseDirectory(_directoryPath);
    }

    private static void DeleteDatabaseDirectory(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }
}
