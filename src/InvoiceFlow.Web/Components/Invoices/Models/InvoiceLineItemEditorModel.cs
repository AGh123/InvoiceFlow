using System.ComponentModel.DataAnnotations;
using InvoiceFlow.Application.Invoices.Dtos;
using InvoiceFlow.Application.Invoices.Requests;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Web.Components.Invoices.Models;

public sealed class InvoiceLineItemEditorModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Description is required.")]
    [MaxLength(InvoiceRules.LineItemDescriptionMaxLength, ErrorMessage = "Description must be 500 characters or fewer.")]
    public string Description { get; set; } = string.Empty;

    [CustomValidation(typeof(InvoiceLineItemEditorModel), nameof(ValidateQuantity))]
    public decimal Quantity { get; set; } = 1m;

    [CustomValidation(typeof(InvoiceLineItemEditorModel), nameof(ValidateUnitPrice))]
    public decimal UnitPrice { get; set; }

    [CustomValidation(typeof(InvoiceLineItemEditorModel), nameof(ValidateDiscountPercent))]
    public decimal DiscountPercent { get; set; }

    public decimal GrossAmount => Quantity * UnitPrice;

    public decimal DiscountAmount => GrossAmount / 100m * DiscountPercent;

    public decimal LineTotal => GrossAmount - DiscountAmount;

    public bool TryCalculateLineTotal(out decimal lineTotal)
    {
        try
        {
            lineTotal = LineTotal;
            return true;
        }
        catch (OverflowException)
        {
            lineTotal = 0m;
            return false;
        }
    }

    public static ValidationResult? ValidateQuantity(object? value)
    {
        var quantity = (decimal)value!;
        if (quantity <= 0m)
        {
            return new ValidationResult("Quantity must be greater than zero.", [nameof(Quantity)]);
        }
        if (quantity > InvoiceRules.MaximumQuantity)
        {
            return new ValidationResult($"Quantity must be at most {InvoiceRules.MaximumQuantity}.", [nameof(Quantity)]);
        }
        if (decimal.Round(quantity, InvoiceRules.QuantityAndUnitPriceScale) != quantity)
        {
            return new ValidationResult($"Quantity must have at most {InvoiceRules.QuantityAndUnitPriceScale} decimal places.", [nameof(Quantity)]);
        }

        return ValidationResult.Success;
    }

    public static ValidationResult? ValidateUnitPrice(object? value)
    {
        var unitPrice = (decimal)value!;
        if (unitPrice < 0m)
        {
            return new ValidationResult("Unit price cannot be negative.", [nameof(UnitPrice)]);
        }
        if (unitPrice > InvoiceRules.MaximumUnitPrice)
        {
            return new ValidationResult($"Unit price must be at most {InvoiceRules.MaximumUnitPrice}.", [nameof(UnitPrice)]);
        }
        if (decimal.Round(unitPrice, InvoiceRules.QuantityAndUnitPriceScale) != unitPrice)
        {
            return new ValidationResult($"Unit price must have at most {InvoiceRules.QuantityAndUnitPriceScale} decimal places.", [nameof(UnitPrice)]);
        }

        return ValidationResult.Success;
    }

    public static ValidationResult? ValidateDiscountPercent(object? value)
    {
        var discountPercent = (decimal)value!;
        if (discountPercent is < 0m or > InvoiceRules.MaximumDiscountPercent)
        {
            return new ValidationResult("Discount must be between 0 and 100%.", [nameof(DiscountPercent)]);
        }
        if (decimal.Round(discountPercent, InvoiceRules.DiscountPercentScale) != discountPercent)
        {
            return new ValidationResult($"Discount must have at most {InvoiceRules.DiscountPercentScale} decimal places.", [nameof(DiscountPercent)]);
        }

        return ValidationResult.Success;
    }

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
