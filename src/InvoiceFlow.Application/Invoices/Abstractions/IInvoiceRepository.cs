using InvoiceFlow.Application.Invoices.Dtos;
using InvoiceFlow.Application.Invoices.Requests;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.Abstractions;

public interface IInvoiceRepository
{
    Task<long> ReserveInvoiceSequenceAsync(CancellationToken cancellationToken = default);

    Task<bool> InvoiceNumberExistsAsync(string invoiceNumber, CancellationToken cancellationToken = default);
    Task<InvoiceListResultDto> GetPageAsync(
        InvoiceListQuery query, CancellationToken cancellationToken = default);

    Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(Guid id, Action<Invoice> update, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
