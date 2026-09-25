using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Infrastructure.Persistence;

public static class DatabaseInitializationExtensions
{
    public static async Task ApplyDatabaseMigrationsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var dbContextFactory = services.GetRequiredService<
            IDbContextFactory<InvoiceFlowDbContext>>();

        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);

        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
