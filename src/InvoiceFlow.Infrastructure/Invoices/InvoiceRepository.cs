using InvoiceFlow.Application.Invoices;
using InvoiceFlow.Application.Invoices.Abstractions;
using InvoiceFlow.Application.Invoices.Dtos;
using InvoiceFlow.Application.Invoices.Exceptions;
using InvoiceFlow.Application.Invoices.Requests;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InvoiceFlow.Infrastructure.Invoices;

public sealed class InvoiceRepository(
    IDbContextFactory<InvoiceFlowDbContext> dbContextFactory) : IInvoiceRepository
{
    private readonly IDbContextFactory<InvoiceFlowDbContext> _dbContextFactory =
        dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));

    public async Task<long> ReserveInvoiceSequenceAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE InvoiceNumberSequence SET Value = Value + 1 WHERE Id = 1 RETURNING Value;";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is long sequence
            ? sequence
            : throw new InvalidOperationException("Invoice number sequence is not initialized.");
    }

    public async Task<bool> InvoiceNumberExistsAsync(
        string invoiceNumber, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Invoices.AsNoTracking()
            .AnyAsync(invoice => invoice.InvoiceNumber == invoiceNumber, cancellationToken);
    }

    public async Task<InvoiceListResultDto> GetPageAsync(
        InvoiceListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.PageSize);

        await using var dbContext =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var invoices = dbContext.Invoices.AsNoTracking();
        var totalCount = await invoices.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return new InvoiceListResultDto([], 0, 0, null, 1);
        }

        var latestIssueDate = await invoices.MaxAsync(invoice => invoice.IssueDate, cancellationToken);
        var search = query.SearchTerm?.Trim() ?? string.Empty;
        var filteredCount = totalCount;
        if (search.Length > 0)
        {
            var pattern = $"%{search.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal)}%";
            invoices = invoices.Where(invoice =>
                EF.Functions.Like(invoice.InvoiceNumber, pattern, "\\") ||
                EF.Functions.Like(invoice.CustomerName, pattern, "\\"));
            filteredCount = await invoices.CountAsync(cancellationToken);
        }

        var pageCount = Math.Max(1, (int)Math.Ceiling(filteredCount / (double)query.PageSize));
        var page = Math.Clamp(query.Page, 1, pageCount);
        var ordered = query.Sort switch
        {
            InvoiceSortOption.Oldest => invoices.OrderBy(invoice => invoice.IssueDate)
                .ThenBy(invoice => EF.Functions.Collate(invoice.InvoiceNumber, "NOCASE"))
                .ThenBy(invoice => invoice.Id),
            InvoiceSortOption.Customer => invoices
                .OrderBy(invoice => EF.Functions.Collate(invoice.CustomerName, "NOCASE"))
                .ThenByDescending(invoice => invoice.IssueDate)
                .ThenBy(invoice => EF.Functions.Collate(invoice.InvoiceNumber, "NOCASE"))
                .ThenBy(invoice => invoice.Id),
            InvoiceSortOption.InvoiceNumber => invoices
                .OrderBy(invoice => EF.Functions.Collate(invoice.InvoiceNumber, "NOCASE"))
                .ThenByDescending(invoice => invoice.IssueDate)
                .ThenBy(invoice => invoice.Id),
            _ => invoices.OrderByDescending(invoice => invoice.IssueDate)
                .ThenBy(invoice => EF.Functions.Collate(invoice.InvoiceNumber, "NOCASE"))
                .ThenBy(invoice => invoice.Id),
        };

        var headers = await ordered
            .Skip((page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(invoice => new InvoiceSummaryDto(invoice.Id, invoice.InvoiceNumber,
                invoice.CustomerName, invoice.IssueDate, invoice.CurrencyCode, 0m))
            .ToArrayAsync(cancellationToken);

        if (headers.Length == 0)
        {
            return new InvoiceListResultDto([], totalCount, filteredCount, latestIssueDate, page);
        }

        var ids = headers.Select(invoice => invoice.Id).ToArray();
        var lineItems = await dbContext.InvoiceLineItems.AsNoTracking()
            .Where(lineItem => ids.Contains(lineItem.InvoiceId))
            .ToArrayAsync(cancellationToken);
        var totals = lineItems.GroupBy(lineItem => lineItem.InvoiceId)
            .ToDictionary(group => group.Key, group => group.Sum(lineItem => lineItem.CalculateLineTotal()));
        var summaries = headers.Select(invoice => invoice with
        {
            GrandTotal = totals.GetValueOrDefault(invoice.Id),
        }).ToArray();

        return new InvoiceListResultDto(summaries, totalCount, filteredCount, latestIssueDate, page);
    }

    public async Task<Invoice?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.Invoices
            .AsNoTracking()
            .Include(invoice => invoice.LineItems)
            .SingleOrDefaultAsync(invoice => invoice.Id == id, cancellationToken);
    }

    public async Task AddAsync(
        Invoice invoice,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        invoice.CalculateTotals();

        await using var dbContext =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        await dbContext.Invoices.AddAsync(invoice, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateInvoiceNumber(exception))
        {
            throw new DuplicateInvoiceNumberException(invoice.InvoiceNumber, exception);
        }
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        Action<Invoice> update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        await using var dbContext =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var persistedInvoice = await dbContext.Invoices
            .Include(existing => existing.LineItems)
            .SingleOrDefaultAsync(existing => existing.Id == id, cancellationToken);
        if (persistedInvoice is null)
        {
            return false;
        }

        update(persistedInvoice);
        persistedInvoice.CalculateTotals();

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.Invoices.Where(invoice => invoice.Id == id)
            .ExecuteDeleteAsync(cancellationToken) > 0;
    }

    private static bool IsDuplicateInvoiceNumber(DbUpdateException exception) =>
        exception.InnerException is SqliteException
        {
            SqliteErrorCode: 19,
            SqliteExtendedErrorCode: 2067,
        } sqliteException &&
        sqliteException.Message.Contains(
            "UNIQUE constraint failed: Invoices.InvoiceNumber",
            StringComparison.Ordinal);
}
