using System.Reflection;

namespace InvoiceFlow.Domain.Tests;

public class InvoiceLineItemTests
{
    [Fact]
    public void Constructor_WithValidValues_CreatesLineItem()
    {
        // Arrange
        var id = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();

        // Act
        var lineItem = new InvoiceLineItem(id, invoiceId, "Consulting", 5m, 100m, 10m);

        // Assert
        Assert.Equal(id, lineItem.Id);
        Assert.Equal(invoiceId, lineItem.InvoiceId);
        Assert.Equal("Consulting", lineItem.Description);
        Assert.Equal(5m, lineItem.Quantity);
        Assert.Equal(100m, lineItem.UnitPrice);
        Assert.Equal(10m, lineItem.DiscountPercent);
    }

    [Fact]
    public void CalculateGrossAmount_MultipliesQuantityByUnitPrice()
    {
        // Arrange
        var lineItem = CreateLineItem(quantity: 5m, unitPrice: 100m, discountPercent: 10m);

        // Act
        var grossAmount = lineItem.CalculateGrossAmount();

        // Assert
        Assert.Equal(500m, grossAmount);
    }

    [Fact]
    public void CalculateDiscountAmount_AppliesPercentageToGrossAmount()
    {
        // Arrange
        var lineItem = CreateLineItem(quantity: 5m, unitPrice: 100m, discountPercent: 10m);

        // Act
        var discountAmount = lineItem.CalculateDiscountAmount();

        // Assert
        Assert.Equal(50m, discountAmount);
    }

    [Fact]
    public void CalculateLineTotal_SubtractsDiscountFromGrossAmount()
    {
        // Arrange
        var lineItem = CreateLineItem(quantity: 5m, unitPrice: 100m, discountPercent: 10m);

        // Act
        var lineTotal = lineItem.CalculateLineTotal();

        // Assert
        Assert.Equal(450m, lineTotal);
    }

    [Fact]
    public void Calculations_WithZeroDiscount_ReturnFullGrossAmount()
    {
        // Arrange
        var lineItem = CreateLineItem(quantity: 2.5m, unitPrice: 19.99m, discountPercent: 0m);

        // Act
        var discountAmount = lineItem.CalculateDiscountAmount();
        var lineTotal = lineItem.CalculateLineTotal();

        // Assert
        Assert.Equal(0m, discountAmount);
        Assert.Equal(49.975m, lineTotal);
    }

    [Fact]
    public void Calculations_WithOneHundredPercentDiscount_ReturnZeroTotal()
    {
        // Arrange
        var lineItem = CreateLineItem(quantity: 3m, unitPrice: 25m, discountPercent: 100m);

        // Act
        var discountAmount = lineItem.CalculateDiscountAmount();
        var lineTotal = lineItem.CalculateLineTotal();

        // Assert
        Assert.Equal(75m, discountAmount);
        Assert.Equal(0m, lineTotal);
    }

    [Fact]
    public void UpdateDetails_WithValidValues_UpdatesAllEditableFields()
    {
        // Arrange
        var lineItem = CreateLineItem(quantity: 1m, unitPrice: 100m, discountPercent: 0m);

        // Act
        lineItem.UpdateDetails("Premium consulting", 2.5m, 125.50m, 15m);

        // Assert
        Assert.Equal("Premium consulting", lineItem.Description);
        Assert.Equal(2.5m, lineItem.Quantity);
        Assert.Equal(125.50m, lineItem.UnitPrice);
        Assert.Equal(15m, lineItem.DiscountPercent);
    }

    [Fact]
    public void UpdateDetails_DoesNotChangeId()
    {
        // Arrange
        var lineItem = CreateLineItem(quantity: 1m, unitPrice: 100m, discountPercent: 0m);
        var originalId = lineItem.Id;

        // Act
        lineItem.UpdateDetails("Premium consulting", 2m, 125m, 10m);

        // Assert
        Assert.Equal(originalId, lineItem.Id);
    }

    [Fact]
    public void UpdateDetails_DoesNotChangeInvoiceId()
    {
        // Arrange
        var lineItem = CreateLineItem(quantity: 1m, unitPrice: 100m, discountPercent: 0m);
        var originalInvoiceId = lineItem.InvoiceId;

        // Act
        lineItem.UpdateDetails("Premium consulting", 2m, 125m, 10m);

        // Assert
        Assert.Equal(originalInvoiceId, lineItem.InvoiceId);
    }

    [Fact]
    public void UpdateDetails_WithInvalidDescription_ThrowsAndPreservesExistingValues()
    {
        // Arrange
        var lineItem = CreateLineItem(quantity: 1m, unitPrice: 100m, discountPercent: 5m);

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            lineItem.UpdateDetails("   ", 2m, 200m, 10m));

        // Assert
        Assert.Equal("description", exception.ParamName);
        AssertDetailsUnchanged(lineItem);
    }

    [Fact]
    public void UpdateDetails_WithInvalidQuantity_ThrowsAndPreservesExistingValues()
    {
        // Arrange
        var lineItem = CreateLineItem(quantity: 1m, unitPrice: 100m, discountPercent: 5m);

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            lineItem.UpdateDetails("Premium consulting", 0m, 200m, 10m));

        // Assert
        Assert.Equal("quantity", exception.ParamName);
        AssertDetailsUnchanged(lineItem);
    }

    [Fact]
    public void UpdateDetails_WithNegativeUnitPrice_ThrowsAndPreservesExistingValues()
    {
        // Arrange
        var lineItem = CreateLineItem(quantity: 1m, unitPrice: 100m, discountPercent: 5m);

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            lineItem.UpdateDetails("Premium consulting", 2m, -0.01m, 10m));

        // Assert
        Assert.Equal("unitPrice", exception.ParamName);
        AssertDetailsUnchanged(lineItem);
    }

    [Fact]
    public void UpdateDetails_WithDiscountBelowZero_ThrowsAndPreservesExistingValues()
    {
        // Arrange
        var lineItem = CreateLineItem(quantity: 1m, unitPrice: 100m, discountPercent: 5m);

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            lineItem.UpdateDetails("Premium consulting", 2m, 200m, -0.01m));

        // Assert
        Assert.Equal("discountPercent", exception.ParamName);
        AssertDetailsUnchanged(lineItem);
    }

    [Fact]
    public void UpdateDetails_WithDiscountAboveOneHundred_ThrowsAndPreservesExistingValues()
    {
        // Arrange
        var lineItem = CreateLineItem(quantity: 1m, unitPrice: 100m, discountPercent: 5m);

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            lineItem.UpdateDetails("Premium consulting", 2m, 200m, 100.01m));

        // Assert
        Assert.Equal("discountPercent", exception.ParamName);
        AssertDetailsUnchanged(lineItem);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveQuantity_Throws(decimal quantity)
    {
        // Arrange
        var id = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new InvoiceLineItem(id, invoiceId, "Consulting", quantity, 100m, 0m));

        // Assert
        Assert.Equal("quantity", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNegativeUnitPrice_Throws()
    {
        // Arrange
        var id = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new InvoiceLineItem(id, invoiceId, "Consulting", 1m, -0.01m, 0m));

        // Assert
        Assert.Equal("unitPrice", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithDiscountBelowZero_Throws()
    {
        // Arrange
        var id = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new InvoiceLineItem(id, invoiceId, "Consulting", 1m, 100m, -0.01m));

        // Assert
        Assert.Equal("discountPercent", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithDiscountAboveOneHundred_Throws()
    {
        // Arrange
        var id = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new InvoiceLineItem(id, invoiceId, "Consulting", 1m, 100m, 100.01m));

        // Assert
        Assert.Equal("discountPercent", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyDescription_Throws(string description)
    {
        // Arrange
        var id = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            new InvoiceLineItem(id, invoiceId, description, 1m, 100m, 0m));

        // Assert
        Assert.Equal("description", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullDescription_Throws()
    {
        // Arrange
        var id = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            new InvoiceLineItem(id, invoiceId, null!, 1m, 100m, 0m));

        // Assert
        Assert.Equal("description", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithEmptyId_Throws()
    {
        // Arrange
        var invoiceId = Guid.NewGuid();

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            new InvoiceLineItem(Guid.Empty, invoiceId, "Consulting", 1m, 100m, 0m));

        // Assert
        Assert.Equal("id", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithEmptyInvoiceId_Throws()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            new InvoiceLineItem(id, Guid.Empty, "Consulting", 1m, 100m, 0m));

        // Assert
        Assert.Equal("invoiceId", exception.ParamName);
    }

    [Fact]
    public void PublicProperties_MatchRequiredDomainProperties()
    {
        // Arrange
        var expectedProperties = new[]
        {
            "Description",
            "DiscountPercent",
            "Id",
            "InvoiceId",
            "Quantity",
            "UnitPrice",
        };

        // Act
        var actualProperties = typeof(InvoiceLineItem)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(property => property.Name)
            .Order()
            .ToArray();

        // Assert
        Assert.Equal(expectedProperties, actualProperties);
    }

    private static InvoiceLineItem CreateLineItem(
        decimal quantity,
        decimal unitPrice,
        decimal discountPercent) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Consulting",
            quantity,
            unitPrice,
            discountPercent);

    private static void AssertDetailsUnchanged(InvoiceLineItem lineItem)
    {
        Assert.Equal("Consulting", lineItem.Description);
        Assert.Equal(1m, lineItem.Quantity);
        Assert.Equal(100m, lineItem.UnitPrice);
        Assert.Equal(5m, lineItem.DiscountPercent);
    }
}
