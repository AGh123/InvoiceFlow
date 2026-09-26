using System.ComponentModel.DataAnnotations;
using InvoiceFlow.Application.Invoices.Dtos;
using InvoiceFlow.Web.Components.Invoices.Models;

namespace InvoiceFlow.Web.Tests.Models;

public class InvoiceEditorModelTests
{
    [Fact]
    public void NewModel_HasValidDefaultsAndAllowsZeroLineItems()
    {
        var dateBeforeConstruction = DateOnly.FromDateTime(DateTime.Today);
        var model = new InvoiceEditorModel
        {
            InvoiceNumber = "INV-001",
            CustomerName = "Acme Ltd",
        };
        var dateAfterConstruction = DateOnly.FromDateTime(DateTime.Today);

        Assert.Equal("USD", model.CurrencyCode);
        Assert.True(model.IssueDate == dateBeforeConstruction || model.IssueDate == dateAfterConstruction);
        Assert.Empty(model.LineItems);
        Assert.Empty(Validate(model));
        Assert.Equal(0m, model.Subtotal);
        Assert.Equal(0m, model.TotalDiscount);
        Assert.Equal(0m, model.GrandTotal);
    }

    [Fact]
    public void FromDetails_MapsInvoiceAndLineItemValues()
    {
        var lineItemId = Guid.NewGuid();
        var details = new InvoiceDetailsDto(
            Guid.NewGuid(),
            "INV-042",
            "Globex",
            new DateOnly(2026, 9, 25),
            "EUR",
            [new InvoiceLineItemDto(lineItemId, "Design", 2.5m, 40m, 10m, 100m, 10m, 90m)],
            100m,
            10m,
            90m);

        var model = InvoiceEditorModel.FromDetails(details);

        Assert.Equal(details.InvoiceNumber, model.InvoiceNumber);
        Assert.Equal(details.CustomerName, model.CustomerName);
        Assert.Equal(details.IssueDate, model.IssueDate);
        Assert.Equal(details.CurrencyCode, model.CurrencyCode);
        var lineItem = Assert.Single(model.LineItems);
        Assert.Equal(lineItemId, lineItem.Id);
        Assert.Equal("Design", lineItem.Description);
        Assert.Equal(2.5m, lineItem.Quantity);
        Assert.Equal(40m, lineItem.UnitPrice);
        Assert.Equal(10m, lineItem.DiscountPercent);
    }

    [Fact]
    public void RequestMappings_OmitIdsForCreateAndPreserveIdsForUpdate()
    {
        var existingId = Guid.NewGuid();
        var model = ValidModel();
        model.LineItems.Add(new InvoiceLineItemEditorModel
        {
            Id = existingId,
            Description = "Consulting",
            Quantity = 3m,
            UnitPrice = 75m,
            DiscountPercent = 20m,
        });

        var create = model.ToCreateRequest();
        var update = model.ToUpdateRequest();

        Assert.Null(Assert.Single(create.LineItems).Id);
        Assert.Equal(existingId, Assert.Single(update.LineItems).Id);
        Assert.Equal(model.InvoiceNumber, create.InvoiceNumber);
        Assert.Equal(model.CustomerName, update.CustomerName);
        Assert.Null(update.GetType().GetProperty(nameof(model.InvoiceNumber)));
    }

    [Fact]
    public void Totals_AggregateLineItemCalculations()
    {
        var model = ValidModel();
        model.LineItems.AddRange(
        [
            new() { Description = "A", Quantity = 2m, UnitPrice = 50m, DiscountPercent = 10m },
            new() { Description = "B", Quantity = 1.5m, UnitPrice = 20m, DiscountPercent = 50m },
        ]);

        Assert.Equal(130m, model.Subtotal);
        Assert.Equal(25m, model.TotalDiscount);
        Assert.Equal(105m, model.GrandTotal);
    }

    [Theory]
    [InlineData("", "Invoice number is required.")]
    [InlineData("123456789012345678901234567890123456789012345678901", "Invoice number must be 50 characters or fewer.")]
    public void InvoiceNumber_UsesWebContractValidation(string value, string expectedMessage)
    {
        var model = ValidModel();
        model.InvoiceNumber = value;

        Assert.Contains(Validate(model), result => result.ErrorMessage == expectedMessage);
    }

    [Fact]
    public void CustomerName_IsRequired()
    {
        var model = ValidModel();
        model.CustomerName = string.Empty;

        Assert.Contains(Validate(model), result => result.ErrorMessage == "Customer name is required.");
    }

    [Theory]
    [InlineData("USD", true)]
    [InlineData("chf", true)]
    [InlineData("US", false)]
    [InlineData("U1D", false)]
    public void CurrencyCode_RequiresThreeLetters(string value, bool isValid)
    {
        var model = ValidModel();
        model.CurrencyCode = value;

        Assert.Equal(isValid, Validate(model).Count == 0);
    }

    private static InvoiceEditorModel ValidModel() => new()
    {
        InvoiceNumber = "INV-001",
        CustomerName = "Acme Ltd",
        IssueDate = new DateOnly(2026, 9, 25),
        CurrencyCode = "USD",
    };

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }
}
