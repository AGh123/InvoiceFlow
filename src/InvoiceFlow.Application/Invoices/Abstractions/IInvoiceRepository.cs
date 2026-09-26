using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.Abstractions;

public interface IInvoiceRepository
{
    Task<long> ReserveInvoiceSequenceAsync(CancellationToken cancellationToken = default);

    Task<bool> InvoiceNumberExistsAsync(string invoiceNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default);

    Task UpdateAsync(Invoice invoice, CancellationToken cancellationToken = default);

    Task DeleteAsync(Invoice invoice, CancellationToken cancellationToken = default);
}
