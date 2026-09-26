using InvoiceFlow.Application.Invoices;
using InvoiceFlow.Application.Invoices.Abstractions;
using InvoiceFlow.Application.Invoices.Dtos;
using InvoiceFlow.Application.Invoices.Requests;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Web.Tests.TestDoubles;

internal sealed class FakeInvoiceRepository : IInvoiceRepository
{
    public List<Invoice> Invoices { get; } = [];
    private long _nextSequence;

    public Task<long> ReserveInvoiceSequenceAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Interlocked.Increment(ref _nextSequence));

    public Task<bool> InvoiceNumberExistsAsync(string invoiceNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(Invoices.Any(invoice => invoice.InvoiceNumber == invoiceNumber));

    public Func<InvoiceListQuery, CancellationToken, Task<InvoiceListResultDto>>? GetPageHandler { get; set; }

    public Func<Guid, CancellationToken, Task<Invoice?>>? GetByIdHandler { get; set; }

    public Func<Invoice, CancellationToken, Task>? AddHandler { get; set; }

    public Func<Invoice, CancellationToken, Task>? UpdateHandler { get; set; }

    public Func<Invoice, CancellationToken, Task>? DeleteHandler { get; set; }

    public int GetPageCallCount { get; private set; }

    public int GetByIdCallCount { get; private set; }

    public int AddCallCount { get; private set; }

    public int UpdateCallCount { get; private set; }

    public int DeleteCallCount { get; private set; }

    public Invoice? LastAdded { get; private set; }

    public Invoice? LastUpdated { get; private set; }

    public Invoice? LastDeleted { get; private set; }

    public Task<InvoiceListResultDto> GetPageAsync(
        InvoiceListQuery request, CancellationToken cancellationToken = default)
    {
        GetPageCallCount++;
        if (GetPageHandler is not null) return GetPageHandler(request, cancellationToken);

        var query = Invoices.Where(invoice =>
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
        return Task.FromResult(new InvoiceListResultDto(summaries, Invoices.Count, filtered.Length,
            Invoices.Count == 0 ? null : Invoices.Max(invoice => invoice.IssueDate), page));
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

    public async Task<bool> UpdateAsync(Guid id, Action<Invoice> update, CancellationToken cancellationToken = default)
    {
        var invoice = Invoices.SingleOrDefault(existing => existing.Id == id);
        if (invoice is null)
        {
            return false;
        }

        update(invoice);
        UpdateCallCount++;
        LastUpdated = invoice;

        if (UpdateHandler is not null)
        {
            await UpdateHandler(invoice, cancellationToken);
        }

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = Invoices.SingleOrDefault(existing => existing.Id == id);
        if (invoice is null)
        {
            return false;
        }

        DeleteCallCount++;
        LastDeleted = invoice;

        if (DeleteHandler is not null)
        {
            await DeleteHandler(invoice, cancellationToken);
            return true;
        }

        Invoices.Remove(invoice);
        return true;
    }
}
