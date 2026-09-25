using System.ComponentModel.DataAnnotations;
using InvoiceFlow.Application.Invoices.Dtos;
using InvoiceFlow.Application.Invoices.Requests;

namespace InvoiceFlow.Web.Components.Invoices.Models;

public sealed class InvoiceLineItemEditorModel
{
    private const string DecimalMaximum = "79228162514264337593543950335";
    private const string DecimalMinimumPositive = "0.0000000000000000000000000001";

    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Description is required.")]
    [MaxLength(500, ErrorMessage = "Description must be 500 characters or fewer.")]
    public string Description { get; set; } = string.Empty;

    [Range(
        typeof(decimal),
        DecimalMinimumPositive,
        DecimalMaximum,
        ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; } = 1m;

    [Range(
        typeof(decimal),
        "0",
        DecimalMaximum,
        ErrorMessage = "Unit price cannot be negative.")]
    public decimal UnitPrice { get; set; }

    [Range(
        typeof(decimal),
        "0",
        "100",
        ErrorMessage = "Discount must be between 0 and 100%.")]
    public decimal DiscountPercent { get; set; }

    public decimal GrossAmount => Quantity * UnitPrice;

    public decimal DiscountAmount => GrossAmount * DiscountPercent / 100m;

    public decimal LineTotal => GrossAmount - DiscountAmount;

    public InvoiceLineItemInput ToInput(bool includeId) =>
        new(
            includeId ? Id : null,
            Description,
            Quantity,
            UnitPrice,
            DiscountPercent);

    public static InvoiceLineItemEditorModel FromDto(InvoiceLineItemDto lineItem) =>
        new()
        {
            Id = lineItem.Id,
            Description = lineItem.Description,
            Quantity = lineItem.Quantity,
            UnitPrice = lineItem.UnitPrice,
            DiscountPercent = lineItem.DiscountPercent,
        };
}
