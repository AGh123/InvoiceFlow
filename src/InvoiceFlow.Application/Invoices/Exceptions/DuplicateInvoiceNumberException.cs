namespace InvoiceFlow.Application.Invoices.Exceptions;

public sealed class DuplicateInvoiceNumberException : Exception
{
    public DuplicateInvoiceNumberException(string invoiceNumber, Exception innerException)
        : base($"An invoice with number '{invoiceNumber}' already exists.", innerException)
    {
        InvoiceNumber = invoiceNumber;
    }

    public string InvoiceNumber { get; }
}
