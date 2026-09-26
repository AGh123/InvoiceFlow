using System.ComponentModel.DataAnnotations;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Web.Components.Invoices.Models;

namespace InvoiceFlow.Web.Tests.Models;

public class InvoiceLineItemEditorModelTests
{
    [Theory]
    [InlineData(2.5, 19.99, 12.5, 49.975, 6.246875, 43.728125)]
    [InlineData(3, 25, 100, 75, 75, 0)]
    [InlineData(2, 0, 50, 0, 0, 0)]
    public void Calculations_HandleDecimalDiscountAndZeroPrice(
        decimal quantity,
        decimal unitPrice,
        decimal discount,
        decimal expectedGross,
        decimal expectedDiscount,
        decimal expectedTotal)
    {
        var model = new InvoiceLineItemEditorModel
        {
            Quantity = quantity,
            UnitPrice = unitPrice,
            DiscountPercent = discount,
        };

        Assert.Equal(expectedGross, model.GrossAmount);
        Assert.Equal(expectedDiscount, model.DiscountAmount);
        Assert.Equal(expectedTotal, model.LineTotal);
    }

    [Theory]
    [InlineData("", 1, 0, 0, "Description is required.")]
    [InlineData("Item", 0, 0, 0, "Quantity must be greater than zero.")]
    [InlineData("Item", 1, -0.01, 0, "Unit price cannot be negative.")]
    [InlineData("Item", 1, 0, -0.01, "Discount must be between 0 and 100%.")]
    [InlineData("Item", 1, 0, 100.01, "Discount must be between 0 and 100%.")]
    public void Validation_RejectsInvalidEditorValues(
        string description,
        decimal quantity,
        decimal unitPrice,
        decimal discount,
        string expectedMessage)
    {
        var model = new InvoiceLineItemEditorModel
        {
            Description = description,
            Quantity = quantity,
            UnitPrice = unitPrice,
            DiscountPercent = discount,
        };

        Assert.Contains(Validate(model), result => result.ErrorMessage == expectedMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Validation_AcceptsDiscountBounds(decimal discount)
    {
        var model = new InvoiceLineItemEditorModel
        {
            Description = "Item",
            Quantity = 1m,
            UnitPrice = 0m,
            DiscountPercent = discount,
        };

        Assert.Empty(Validate(model));
    }

    [Fact]
    public void Validation_RejectsExcessNumericScaleAndValuesAboveStorageMaximum()
    {
        var model = new InvoiceLineItemEditorModel
        {
            Description = "Item",
            Quantity = 1.00001m,
            UnitPrice = InvoiceRules.MaximumUnitPrice + 0.0001m,
            DiscountPercent = 1.001m,
        };

        var messages = Validate(model);

        Assert.Contains(messages, result => result.MemberNames.Contains(nameof(model.Quantity)) &&
            result.ErrorMessage!.Contains("4 decimal places", StringComparison.Ordinal));
        Assert.Contains(messages, result => result.MemberNames.Contains(nameof(model.UnitPrice)) &&
            result.ErrorMessage!.Contains("at most", StringComparison.Ordinal));
        Assert.Contains(messages, result => result.MemberNames.Contains(nameof(model.DiscountPercent)) &&
            result.ErrorMessage!.Contains("2 decimal places", StringComparison.Ordinal));
    }

    [Fact]
    public void Validation_RejectsQuantityAboveMaximumAndExcessUnitPriceScale()
    {
        var model = new InvoiceLineItemEditorModel
        {
            Description = "Item",
            Quantity = InvoiceRules.MaximumQuantity + 0.0001m,
            UnitPrice = 1.00001m,
        };

        var messages = Validate(model);

        Assert.Contains(messages, result => result.MemberNames.Contains(nameof(model.Quantity)));
        Assert.Contains(messages, result => result.MemberNames.Contains(nameof(model.UnitPrice)) &&
            result.ErrorMessage!.Contains("4 decimal places", StringComparison.Ordinal));
    }

    [Fact]
    public void MaximumStoredValues_AreValidAndDiscountCalculationDoesNotOverflow()
    {
        var model = new InvoiceLineItemEditorModel
        {
            Description = "Maximum",
            Quantity = InvoiceRules.MaximumQuantity,
            UnitPrice = InvoiceRules.MaximumUnitPrice,
            DiscountPercent = InvoiceRules.MaximumDiscountPercent,
        };

        Assert.Empty(Validate(model));
        Assert.Equal(model.GrossAmount, model.DiscountAmount);
        Assert.True(model.TryCalculateLineTotal(out var lineTotal));
        Assert.Equal(0m, lineTotal);
    }

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }
}
