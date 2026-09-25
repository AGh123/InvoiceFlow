using InvoiceFlow.Application.Invoices.Abstractions;
using InvoiceFlow.Application.Invoices.Exceptions;
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

    public async Task<IReadOnlyList<Invoice>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.Invoices
            .AsNoTracking()
            .Include(invoice => invoice.LineItems)
            .OrderByDescending(invoice => invoice.IssueDate)
            .ThenBy(invoice => invoice.InvoiceNumber)
            .ToArrayAsync(cancellationToken);
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

    public async Task UpdateAsync(
        Invoice invoice,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        await using var dbContext =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var persistedInvoice = await dbContext.Invoices
            .Include(existing => existing.LineItems)
            .SingleOrDefaultAsync(existing => existing.Id == invoice.Id, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Cannot update invoice '{invoice.Id}' because it is not stored.");

        persistedInvoice.UpdateDetails(
            invoice.InvoiceNumber,
            invoice.CustomerName,
            invoice.IssueDate,
            invoice.CurrencyCode);

        var suppliedLineItems = invoice.LineItems.ToDictionary(lineItem => lineItem.Id);

        foreach (var persistedLineItem in persistedInvoice.LineItems
                     .Where(lineItem => !suppliedLineItems.ContainsKey(lineItem.Id))
                     .ToArray())
        {
            persistedInvoice.RemoveLineItem(persistedLineItem.Id);
        }

        foreach (var suppliedLineItem in suppliedLineItems.Values)
        {
            if (suppliedLineItem.InvoiceId != invoice.Id)
            {
                throw new InvalidOperationException(
                    $"Line item '{suppliedLineItem.Id}' does not belong to invoice '{invoice.Id}'.");
            }

            var persistedLineItem = persistedInvoice.LineItems
                .SingleOrDefault(lineItem => lineItem.Id == suppliedLineItem.Id);

            if (persistedLineItem is null)
            {
                persistedInvoice.AddLineItem(
                    suppliedLineItem.Id,
                    suppliedLineItem.Description,
                    suppliedLineItem.Quantity,
                    suppliedLineItem.UnitPrice,
                    suppliedLineItem.DiscountPercent);

                continue;
            }

            persistedLineItem.UpdateDetails(
                suppliedLineItem.Description,
                suppliedLineItem.Quantity,
                suppliedLineItem.UnitPrice,
                suppliedLineItem.DiscountPercent);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateInvoiceNumber(exception))
        {
            throw new DuplicateInvoiceNumberException(invoice.InvoiceNumber, exception);
        }
    }

    public async Task DeleteAsync(
        Invoice invoice,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        await using var dbContext =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var persistedInvoice = await dbContext.Invoices
            .SingleOrDefaultAsync(existing => existing.Id == invoice.Id, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Cannot delete invoice '{invoice.Id}' because it is not stored.");

        dbContext.Invoices.Remove(persistedInvoice);
        await dbContext.SaveChangesAsync(cancellationToken);
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
