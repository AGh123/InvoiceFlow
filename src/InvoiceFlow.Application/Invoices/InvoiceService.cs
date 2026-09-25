using InvoiceFlow.Application.Invoices.Abstractions;
using InvoiceFlow.Application.Invoices.Dtos;
using InvoiceFlow.Application.Invoices.Requests;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices;

public sealed class InvoiceService(IInvoiceRepository invoiceRepository)
{
    private readonly IInvoiceRepository _invoiceRepository =
        invoiceRepository ?? throw new ArgumentNullException(nameof(invoiceRepository));

    public async Task<IReadOnlyList<InvoiceSummaryDto>> GetInvoicesAsync(
        CancellationToken cancellationToken = default)
    {
        var invoices = await _invoiceRepository.GetAllAsync(cancellationToken);

        return invoices.Select(MapSummary).ToArray();
    }

    public async Task<InvoiceDetailsDto?> GetInvoiceAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(id, cancellationToken);

        return invoice is null ? null : MapDetails(invoice);
    }

    public async Task<InvoiceDetailsDto> CreateInvoiceAsync(
        CreateInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var lineItemInputs = SnapshotLineItems(request.LineItems);

        if (lineItemInputs.Any(input => input.Id.HasValue))
        {
            throw new ArgumentException(
                "Line item IDs must not be supplied when creating an invoice.",
                nameof(request));
        }

        var invoice = new Invoice(
            Guid.NewGuid(),
            request.InvoiceNumber,
            request.CustomerName,
            request.IssueDate,
            request.CurrencyCode);

        foreach (var input in lineItemInputs)
        {
            invoice.AddLineItem(
                Guid.NewGuid(),
                input.Description,
                input.Quantity,
                input.UnitPrice,
                input.DiscountPercent);
        }

        await _invoiceRepository.AddAsync(invoice, cancellationToken);

        return MapDetails(invoice);
    }

    public async Task<bool> UpdateInvoiceAsync(
        Guid id,
        UpdateInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var invoice = await _invoiceRepository.GetByIdAsync(id, cancellationToken);
        if (invoice is null)
        {
            return false;
        }

        var lineItemInputs = SnapshotLineItems(request.LineItems);
        var proposedState = BuildValidatedProposedState(invoice, request, lineItemInputs);

        ApplyProposedState(invoice, proposedState);
        await _invoiceRepository.UpdateAsync(invoice, cancellationToken);

        return true;
    }

    public async Task<bool> DeleteInvoiceAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(id, cancellationToken);
        if (invoice is null)
        {
            return false;
        }

        await _invoiceRepository.DeleteAsync(invoice, cancellationToken);

        return true;
    }

    private static Invoice BuildValidatedProposedState(
        Invoice existingInvoice,
        UpdateInvoiceRequest request,
        IReadOnlyList<InvoiceLineItemInput> lineItemInputs)
    {
        ValidateExistingLineItemIds(existingInvoice, lineItemInputs);

        var proposedState = new Invoice(
            existingInvoice.Id,
            request.InvoiceNumber,
            request.CustomerName,
            request.IssueDate,
            request.CurrencyCode);

        foreach (var input in lineItemInputs)
        {
            proposedState.AddLineItem(
                input.Id ?? Guid.NewGuid(),
                input.Description,
                input.Quantity,
                input.UnitPrice,
                input.DiscountPercent);
        }

        return proposedState;
    }

    private static void ValidateExistingLineItemIds(
        Invoice invoice,
        IReadOnlyList<InvoiceLineItemInput> lineItemInputs)
    {
        var existingIds = invoice.LineItems.Select(lineItem => lineItem.Id).ToHashSet();
        var suppliedIds = new HashSet<Guid>();

        foreach (var input in lineItemInputs.Where(input => input.Id.HasValue))
        {
            var suppliedId = input.Id!.Value;

            if (!suppliedIds.Add(suppliedId))
            {
                throw new ArgumentException(
                    $"Line item ID '{suppliedId}' was supplied more than once.",
                    nameof(lineItemInputs));
            }

            if (!existingIds.Contains(suppliedId))
            {
                throw new ArgumentException(
                    $"Line item ID '{suppliedId}' does not belong to invoice '{invoice.Id}'.",
                    nameof(lineItemInputs));
            }
        }
    }

    private static void ApplyProposedState(Invoice invoice, Invoice proposedState)
    {
        invoice.UpdateDetails(
            proposedState.InvoiceNumber,
            proposedState.CustomerName,
            proposedState.IssueDate,
            proposedState.CurrencyCode);

        var requestedIds = proposedState.LineItems
            .Select(lineItem => lineItem.Id)
            .ToHashSet();

        foreach (var existingLineItem in invoice.LineItems
                     .Where(lineItem => !requestedIds.Contains(lineItem.Id))
                     .ToArray())
        {
            invoice.RemoveLineItem(existingLineItem.Id);
        }

        foreach (var requestedLineItem in proposedState.LineItems)
        {
            var existingLineItem = invoice.LineItems
                .SingleOrDefault(lineItem => lineItem.Id == requestedLineItem.Id);

            if (existingLineItem is null)
            {
                invoice.AddLineItem(
                    requestedLineItem.Id,
                    requestedLineItem.Description,
                    requestedLineItem.Quantity,
                    requestedLineItem.UnitPrice,
                    requestedLineItem.DiscountPercent);

                continue;
            }

            existingLineItem.UpdateDetails(
                requestedLineItem.Description,
                requestedLineItem.Quantity,
                requestedLineItem.UnitPrice,
                requestedLineItem.DiscountPercent);
        }
    }

    private static InvoiceLineItemInput[] SnapshotLineItems(
        IReadOnlyList<InvoiceLineItemInput> lineItems)
    {
        ArgumentNullException.ThrowIfNull(lineItems);

        return [.. lineItems];
    }

    private static InvoiceSummaryDto MapSummary(Invoice invoice) =>
        new(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.CustomerName,
            invoice.IssueDate,
            invoice.CurrencyCode,
            invoice.CalculateGrandTotal());

    private static InvoiceDetailsDto MapDetails(Invoice invoice) =>
        new(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.CustomerName,
            invoice.IssueDate,
            invoice.CurrencyCode,
            invoice.LineItems.Select(MapLineItem).ToArray(),
            invoice.CalculateSubtotal(),
            invoice.CalculateTotalDiscount(),
            invoice.CalculateGrandTotal());

    private static InvoiceLineItemDto MapLineItem(InvoiceLineItem lineItem) =>
        new(
            lineItem.Id,
            lineItem.Description,
            lineItem.Quantity,
            lineItem.UnitPrice,
            lineItem.DiscountPercent,
            lineItem.CalculateGrossAmount(),
            lineItem.CalculateDiscountAmount(),
            lineItem.CalculateLineTotal());
}
