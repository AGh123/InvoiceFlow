using InvoiceFlow.Application.Invoices.Abstractions;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Tests.Fakes;

internal sealed class FakeInvoiceRepository : IInvoiceRepository
{
    private readonly List<Invoice> _invoices = [];

    public IReadOnlyList<Invoice> Invoices => _invoices.AsReadOnly();

    public int AddCallCount { get; private set; }

    public int UpdateCallCount { get; private set; }

    public int DeleteCallCount { get; private set; }

    public void Seed(Invoice invoice) => _invoices.Add(invoice);

    public Task<IReadOnlyList<Invoice>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Invoice>>(_invoices.ToArray());

    public Task<Invoice?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_invoices.SingleOrDefault(invoice => invoice.Id == id));

    public Task AddAsync(
        Invoice invoice,
        CancellationToken cancellationToken = default)
    {
        AddCallCount++;
        _invoices.Add(invoice);

        return Task.CompletedTask;
    }

    public Task UpdateAsync(
        Invoice invoice,
        CancellationToken cancellationToken = default)
    {
        UpdateCallCount++;
        var index = _invoices.FindIndex(existing => existing.Id == invoice.Id);

        if (index < 0)
        {
            throw new InvalidOperationException("Cannot update an invoice that is not stored.");
        }

        _invoices[index] = invoice;

        return Task.CompletedTask;
    }

    public Task DeleteAsync(
        Invoice invoice,
        CancellationToken cancellationToken = default)
    {
        DeleteCallCount++;
        _invoices.Remove(invoice);

        return Task.CompletedTask;
    }
}
