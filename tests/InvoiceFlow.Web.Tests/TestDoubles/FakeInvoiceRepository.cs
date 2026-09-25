using InvoiceFlow.Application.Invoices.Abstractions;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Web.Tests.TestDoubles;

internal sealed class FakeInvoiceRepository : IInvoiceRepository
{
    public List<Invoice> Invoices { get; } = [];

    public Func<CancellationToken, Task<IReadOnlyList<Invoice>>>? GetAllHandler { get; set; }

    public Func<Guid, CancellationToken, Task<Invoice?>>? GetByIdHandler { get; set; }

    public Func<Invoice, CancellationToken, Task>? AddHandler { get; set; }

    public Func<Invoice, CancellationToken, Task>? UpdateHandler { get; set; }

    public Func<Invoice, CancellationToken, Task>? DeleteHandler { get; set; }

    public int GetAllCallCount { get; private set; }

    public int GetByIdCallCount { get; private set; }

    public int AddCallCount { get; private set; }

    public int UpdateCallCount { get; private set; }

    public int DeleteCallCount { get; private set; }

    public Invoice? LastAdded { get; private set; }

    public Invoice? LastUpdated { get; private set; }

    public Invoice? LastDeleted { get; private set; }

    public Task<IReadOnlyList<Invoice>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        GetAllCallCount++;
        return GetAllHandler?.Invoke(cancellationToken) ??
               Task.FromResult<IReadOnlyList<Invoice>>([.. Invoices]);
    }

    public Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdCallCount++;
        return GetByIdHandler?.Invoke(id, cancellationToken) ??
               Task.FromResult(Invoices.SingleOrDefault(invoice => invoice.Id == id));
    }

    public async Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        AddCallCount++;
        LastAdded = invoice;

        if (AddHandler is not null)
        {
            await AddHandler(invoice, cancellationToken);
            return;
        }

        Invoices.Add(invoice);
    }

    public async Task UpdateAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        UpdateCallCount++;
        LastUpdated = invoice;

        if (UpdateHandler is not null)
        {
            await UpdateHandler(invoice, cancellationToken);
        }
    }

    public async Task DeleteAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        DeleteCallCount++;
        LastDeleted = invoice;

        if (DeleteHandler is not null)
        {
            await DeleteHandler(invoice, cancellationToken);
            return;
        }

        Invoices.Remove(invoice);
    }
}
