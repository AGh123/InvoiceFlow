using System.ComponentModel.DataAnnotations;
using InvoiceFlow.Application.Invoices.Dtos;
using InvoiceFlow.Application.Invoices.Requests;

namespace InvoiceFlow.Web.Components.Invoices.Models;

[Microsoft.Extensions.Validation.Embedded.ValidatableType]
public sealed class InvoiceEditorModel
{
    [Required(ErrorMessage = "Invoice number is required.")]
    [MaxLength(50, ErrorMessage = "Invoice number must be 50 characters or fewer.")]
    public string InvoiceNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Customer name is required.")]
    [MaxLength(200, ErrorMessage = "Customer name must be 200 characters or fewer.")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Issue date is required.")]
    public DateOnly IssueDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required(ErrorMessage = "Currency is required.")]
    [RegularExpression(
        "^[A-Za-z]{3}$",
        ErrorMessage = "Currency must be a three-letter code.")]
    public string CurrencyCode { get; set; } = "USD";

    public List<InvoiceLineItemEditorModel> LineItems { get; set; } = [];

    public decimal Subtotal => LineItems.Sum(lineItem => lineItem.GrossAmount);

    public decimal TotalDiscount => LineItems.Sum(lineItem => lineItem.DiscountAmount);

    public decimal GrandTotal => LineItems.Sum(lineItem => lineItem.LineTotal);

    public CreateInvoiceRequest ToCreateRequest() =>
        new(
            InvoiceNumber,
            CustomerName,
            IssueDate,
            CurrencyCode,
            LineItems.Select(lineItem => lineItem.ToInput(includeId: false)).ToArray());

    public UpdateInvoiceRequest ToUpdateRequest() =>
        new(
            InvoiceNumber,
            CustomerName,
            IssueDate,
            CurrencyCode,
            LineItems.Select(lineItem => lineItem.ToInput(includeId: true)).ToArray());

    public static InvoiceEditorModel FromDetails(InvoiceDetailsDto invoice) =>
        new()
        {
            InvoiceNumber = invoice.InvoiceNumber,
            CustomerName = invoice.CustomerName,
            IssueDate = invoice.IssueDate,
            CurrencyCode = invoice.CurrencyCode,
            LineItems = invoice.LineItems
                .Select(InvoiceLineItemEditorModel.FromDto)
                .ToList(),
        };
}
