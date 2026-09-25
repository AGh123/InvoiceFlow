using InvoiceFlow.Application.Invoices.Abstractions;
using InvoiceFlow.Infrastructure.Invoices;
using InvoiceFlow.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string databasePath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var absoluteDatabasePath = Path.GetFullPath(databasePath);
        var databaseDirectory = Path.GetDirectoryName(absoluteDatabasePath)
            ?? throw new ArgumentException(
                "The database path must include a directory.",
                nameof(databasePath));

        Directory.CreateDirectory(databaseDirectory);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = absoluteDatabasePath,
            ForeignKeys = true,
        }.ToString();

        services.AddDbContextFactory<InvoiceFlowDbContext>(options =>
            options.UseSqlite(connectionString));
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();

        return services;
    }
}
