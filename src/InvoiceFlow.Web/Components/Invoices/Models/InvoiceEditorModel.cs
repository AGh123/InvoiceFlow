using System.ComponentModel.DataAnnotations;
using InvoiceFlow.Application.Invoices.Dtos;
using InvoiceFlow.Application.Invoices.Requests;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Web.Components.Invoices.Models;

[Microsoft.Extensions.Validation.Embedded.ValidatableType]
public sealed class InvoiceEditorModel
{
    public string? GeneratedInvoiceNumber { get; set; }
    [Required(ErrorMessage = "Invoice number is required.")]
    [MaxLength(InvoiceRules.InvoiceNumberMaxLength, ErrorMessage = "Invoice number must be 50 characters or fewer.")]
    public string InvoiceNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Customer name is required.")]
    [MaxLength(InvoiceRules.CustomerNameMaxLength, ErrorMessage = "Customer name must be 200 characters or fewer.")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Issue date is required.")]
    public DateOnly IssueDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required(ErrorMessage = "Currency is required.")]
    [RegularExpression(
        "^[A-Za-z]{3}$",
        ErrorMessage = "Currency must be a three-letter code.")]
    public string CurrencyCode { get; set; } = "USD";

    public List<InvoiceLineItemEditorModel> LineItems { get; set; } = [];

    public (decimal Subtotal, decimal TotalDiscount, decimal GrandTotal) CalculateTotals()
    {
        var subtotal = 0m;
        var discount = 0m;
        var grandTotal = 0m;
        foreach (var lineItem in LineItems)
        {
            subtotal += lineItem.GrossAmount;
            discount += lineItem.DiscountAmount;
            grandTotal += lineItem.LineTotal;
        }

        return (subtotal, discount, grandTotal);
    }

    public bool TryCalculateTotals(out (decimal Subtotal, decimal TotalDiscount, decimal GrandTotal) totals)
    {
        try
        {
            totals = CalculateTotals();
            return true;
        }
        catch (OverflowException)
        {
            totals = default;
            return false;
        }
    }

    public CreateInvoiceRequest ToCreateRequest() =>
        new(
            InvoiceNumber,
            CustomerName,
            IssueDate,
            CurrencyCode,
            LineItems.Select(lineItem => lineItem.ToInput(includeId: false)).ToArray(),
            InvoiceNumber == GeneratedInvoiceNumber);

    public UpdateInvoiceRequest ToUpdateRequest() =>
        new(
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
