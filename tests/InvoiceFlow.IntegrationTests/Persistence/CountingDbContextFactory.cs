using InvoiceFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InvoiceFlow.IntegrationTests.Persistence;

internal sealed class CountingDbContextFactory(
    IDbContextFactory<InvoiceFlowDbContext> innerFactory)
    : IDbContextFactory<InvoiceFlowDbContext>
{
    private readonly IDbContextFactory<InvoiceFlowDbContext> _innerFactory = innerFactory;
    private readonly List<InvoiceFlowDbContext> _createdContexts = [];

    public IReadOnlyList<InvoiceFlowDbContext> CreatedContexts => _createdContexts;

    public InvoiceFlowDbContext CreateDbContext()
    {
        var dbContext = _innerFactory.CreateDbContext();
        _createdContexts.Add(dbContext);
        return dbContext;
    }

    public async Task<InvoiceFlowDbContext> CreateDbContextAsync(
        CancellationToken cancellationToken = default)
    {
        var dbContext = await _innerFactory.CreateDbContextAsync(cancellationToken);
        _createdContexts.Add(dbContext);
        return dbContext;
    }
}
