using InvoiceFlow.Application.Invoices;
using InvoiceFlow.Application.Invoices.Abstractions;
using InvoiceFlow.Application.Invoices.Dtos;
using InvoiceFlow.Application.Invoices.Exceptions;
using InvoiceFlow.Application.Invoices.Requests;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Tests.Fakes;

internal sealed class FakeInvoiceRepository : IInvoiceRepository
{
    private readonly List<Invoice> _invoices = [];
    private long _nextSequence;

    public Task<long> ReserveInvoiceSequenceAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Interlocked.Increment(ref _nextSequence));

    public Task<bool> InvoiceNumberExistsAsync(string invoiceNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(_invoices.Any(invoice => invoice.InvoiceNumber == invoiceNumber));

    public IReadOnlyList<Invoice> Invoices => _invoices.AsReadOnly();

    public int AddCallCount { get; private set; }

    public int UpdateCallCount { get; private set; }

    public int DeleteCallCount { get; private set; }

    public int GetByIdCallCount { get; private set; }

    public Exception? DeleteFailure { get; set; }

    public void Seed(Invoice invoice) => _invoices.Add(invoice);

    public Task<InvoiceListResultDto> GetPageAsync(
        InvoiceListQuery request, CancellationToken cancellationToken = default)
    {
        var query = _invoices.Where(invoice =>
            invoice.InvoiceNumber.Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
            invoice.CustomerName.Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase));
        query = request.Sort switch
        {
            InvoiceSortOption.Oldest => query.OrderBy(invoice => invoice.IssueDate)
                .ThenBy(invoice => invoice.InvoiceNumber, StringComparer.OrdinalIgnoreCase),
            InvoiceSortOption.Customer => query.OrderBy(invoice => invoice.CustomerName, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(invoice => invoice.IssueDate),
            InvoiceSortOption.InvoiceNumber => query.OrderBy(invoice => invoice.InvoiceNumber, StringComparer.OrdinalIgnoreCase),
            _ => query.OrderByDescending(invoice => invoice.IssueDate)
                .ThenBy(invoice => invoice.InvoiceNumber, StringComparer.OrdinalIgnoreCase),
        };
        var filtered = query.ToArray();
        var pageCount = Math.Max(1, (filtered.Length + request.PageSize - 1) / request.PageSize);
        var page = Math.Clamp(request.Page, 1, pageCount);
        var summaries = filtered.Skip((page - 1) * request.PageSize).Take(request.PageSize)
            .Select(invoice => new InvoiceSummaryDto(invoice.Id, invoice.InvoiceNumber,
                invoice.CustomerName, invoice.IssueDate, invoice.CurrencyCode, invoice.CalculateGrandTotal()))
            .ToArray();
        return Task.FromResult(new InvoiceListResultDto(summaries, _invoices.Count, filtered.Length,
            _invoices.Count == 0 ? null : _invoices.Max(invoice => invoice.IssueDate), page));
    }

    public Task<Invoice?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        GetByIdCallCount++;
        return Task.FromResult(_invoices.SingleOrDefault(invoice => invoice.Id == id));
    }

    public Task AddAsync(
        Invoice invoice,
        CancellationToken cancellationToken = default)
    {
        AddCallCount++;
        if (_invoices.Any(existing => existing.InvoiceNumber == invoice.InvoiceNumber))
        {
            throw new DuplicateInvoiceNumberException(invoice.InvoiceNumber, new InvalidOperationException("Duplicate"));
        }
        _invoices.Add(invoice);

        return Task.CompletedTask;
    }

    public Task<bool> UpdateAsync(
        Guid id,
        Action<Invoice> update,
        CancellationToken cancellationToken = default)
    {
        var invoice = _invoices.SingleOrDefault(existing => existing.Id == id);
        if (invoice is null)
        {
            return Task.FromResult(false);
        }

        update(invoice);
        UpdateCallCount++;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (DeleteFailure is not null)
        {
            throw DeleteFailure;
        }

        var removed = _invoices.RemoveAll(invoice => invoice.Id == id) > 0;
        if (removed)
        {
            DeleteCallCount++;
        }

        return Task.FromResult(removed);
    }
}
