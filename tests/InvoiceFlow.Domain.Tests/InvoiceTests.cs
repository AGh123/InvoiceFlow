using System.Reflection;

namespace InvoiceFlow.Domain.Tests;

public class InvoiceTests
{
    private static readonly DateOnly DefaultIssueDate = new(2026, 9, 25);

    [Fact]
    public void Constructor_WithValidValues_CreatesInvoice()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var invoice = new Invoice(id, "INV-001", "Acme Ltd", DefaultIssueDate, "USD");

        // Assert
        Assert.Equal(id, invoice.Id);
        Assert.Equal("INV-001", invoice.InvoiceNumber);
        Assert.Equal("Acme Ltd", invoice.CustomerName);
        Assert.Equal(DefaultIssueDate, invoice.IssueDate);
        Assert.Equal("USD", invoice.CurrencyCode);
        Assert.Empty(invoice.LineItems);
    }

    [Fact]
    public void Constructor_WithLowercaseCurrencyCode_NormalizesToUppercase()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var invoice = new Invoice(id, "INV-001", "Acme Ltd", DefaultIssueDate, "eur");

        // Assert
        Assert.Equal("EUR", invoice.CurrencyCode);
    }

    [Fact]
    public void Constructor_WithEmptyId_Throws()
    {
        // Arrange
        var id = Guid.Empty;

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            new Invoice(id, "INV-001", "Acme Ltd", DefaultIssueDate, "USD"));

        // Assert
        Assert.Equal("id", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidInvoiceNumber_Throws(string invoiceNumber)
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            new Invoice(id, invoiceNumber, "Acme Ltd", DefaultIssueDate, "USD"));

        // Assert
        Assert.Equal("invoiceNumber", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullInvoiceNumber_Throws()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            new Invoice(id, null!, "Acme Ltd", DefaultIssueDate, "USD"));

        // Assert
        Assert.Equal("invoiceNumber", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidCustomerName_Throws(string customerName)
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            new Invoice(id, "INV-001", customerName, DefaultIssueDate, "USD"));

        // Assert
        Assert.Equal("customerName", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullCustomerName_Throws()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            new Invoice(id, "INV-001", null!, DefaultIssueDate, "USD"));

        // Assert
        Assert.Equal("customerName", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("U1D")]
    [InlineData("€UR")]
    [InlineData(" USD ")]
    public void Constructor_WithInvalidCurrencyCode_Throws(string currencyCode)
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            new Invoice(id, "INV-001", "Acme Ltd", DefaultIssueDate, currencyCode));

        // Assert
        Assert.Equal("currencyCode", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullCurrencyCode_Throws()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            new Invoice(id, "INV-001", "Acme Ltd", DefaultIssueDate, null!));

        // Assert
        Assert.Equal("currencyCode", exception.ParamName);
    }

    [Fact]
    public void AddLineItem_WithValidValues_AddsAssociatedLineItem()
    {
        // Arrange
        var invoice = CreateInvoice();
        var lineItemId = Guid.NewGuid();

        // Act
        var lineItem = invoice.AddLineItem(lineItemId, "Consulting", 5m, 100m, 10m);

        // Assert
        Assert.Single(invoice.LineItems);
        Assert.Same(lineItem, invoice.LineItems[0]);
        Assert.Equal(lineItemId, lineItem.Id);
        Assert.Equal(invoice.Id, lineItem.InvoiceId);
    }

    [Fact]
    public void AddLineItem_WithDuplicateId_Throws()
    {
        // Arrange
        var invoice = CreateInvoice();
        var lineItemId = Guid.NewGuid();
        invoice.AddLineItem(lineItemId, "Consulting", 1m, 100m, 0m);

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            invoice.AddLineItem(lineItemId, "Support", 1m, 50m, 0m));

        // Assert
        Assert.Equal("id", exception.ParamName);
        Assert.Single(invoice.LineItems);
    }

    [Fact]
    public void LineItems_ExposesReadOnlyViewThatCannotModifyInvoice()
    {
        // Arrange
        var invoice = CreateInvoice();
        var existingLineItem = invoice.AddLineItem(
            Guid.NewGuid(),
            "Consulting",
            1m,
            100m,
            0m);
        var externalLineItem = new InvoiceLineItem(
            Guid.NewGuid(),
            invoice.Id,
            "Support",
            1m,
            50m,
            0m);

        // Act
        var exposedPropertyType = typeof(Invoice)
            .GetProperty(nameof(Invoice.LineItems))!
            .PropertyType;
        var collectionView = Assert.IsAssignableFrom<ICollection<InvoiceLineItem>>(
            invoice.LineItems);
        Assert.Throws<NotSupportedException>(() =>
            collectionView.Add(externalLineItem));

        // Assert
        Assert.Equal(typeof(IReadOnlyList<InvoiceLineItem>), exposedPropertyType);
        Assert.True(collectionView.IsReadOnly);
        Assert.Single(invoice.LineItems);
        Assert.Same(existingLineItem, invoice.LineItems[0]);
    }

    [Fact]
    public void RemoveLineItem_WithExistingId_RemovesItemAndReturnsTrue()
    {
        // Arrange
        var invoice = CreateInvoice();
        var lineItem = invoice.AddLineItem(Guid.NewGuid(), "Consulting", 1m, 100m, 0m);

        // Act
        var removed = invoice.RemoveLineItem(lineItem.Id);

        // Assert
        Assert.True(removed);
        Assert.Empty(invoice.LineItems);
    }

    [Fact]
    public void RemoveLineItem_WithMissingId_ReturnsFalse()
    {
        // Arrange
        var invoice = CreateInvoice();
        invoice.AddLineItem(Guid.NewGuid(), "Consulting", 1m, 100m, 0m);

        // Act
        var removed = invoice.RemoveLineItem(Guid.NewGuid());

        // Assert
        Assert.False(removed);
        Assert.Single(invoice.LineItems);
    }

    [Fact]
    public void CalculateSubtotal_SumsLineItemGrossAmounts()
    {
        // Arrange
        var invoice = CreateInvoice();
        invoice.AddLineItem(Guid.NewGuid(), "Consulting", 5m, 100m, 10m);
        invoice.AddLineItem(Guid.NewGuid(), "Support", 2m, 75m, 20m);

        // Act
        var subtotal = invoice.CalculateSubtotal();

        // Assert
        Assert.Equal(650m, subtotal);
    }

    [Fact]
    public void CalculateTotalDiscount_SumsLineItemDiscounts()
    {
        // Arrange
        var invoice = CreateInvoice();
        invoice.AddLineItem(Guid.NewGuid(), "Consulting", 5m, 100m, 10m);
        invoice.AddLineItem(Guid.NewGuid(), "Support", 2m, 75m, 20m);

        // Act
        var totalDiscount = invoice.CalculateTotalDiscount();

        // Assert
        Assert.Equal(80m, totalDiscount);
    }

    [Fact]
    public void CalculateGrandTotal_SumsDiscountedLineTotals()
    {
        // Arrange
        var invoice = CreateInvoice();
        invoice.AddLineItem(Guid.NewGuid(), "Consulting", 5m, 100m, 10m);
        invoice.AddLineItem(Guid.NewGuid(), "Support", 2m, 75m, 20m);

        // Act
        var grandTotal = invoice.CalculateGrandTotal();

        // Assert
        Assert.Equal(570m, grandTotal);
    }

    [Fact]
    public void Calculations_WithNoLineItems_ReturnZeroTotals()
    {
        // Arrange
        var invoice = CreateInvoice();

        // Act
        var subtotal = invoice.CalculateSubtotal();
        var totalDiscount = invoice.CalculateTotalDiscount();
        var grandTotal = invoice.CalculateGrandTotal();

        // Assert
        Assert.Equal(0m, subtotal);
        Assert.Equal(0m, totalDiscount);
        Assert.Equal(0m, grandTotal);
    }

    [Fact]
    public void UpdateDetails_WithValidValues_UpdatesEditableDetails()
    {
        // Arrange
        var invoice = CreateInvoice();
        var updatedIssueDate = new DateOnly(2027, 1, 15);

        // Act
        invoice.UpdateDetails("INV-002", "Globex Corp", updatedIssueDate, "gbp");

        // Assert
        Assert.Equal("INV-002", invoice.InvoiceNumber);
        Assert.Equal("Globex Corp", invoice.CustomerName);
        Assert.Equal(updatedIssueDate, invoice.IssueDate);
        Assert.Equal("GBP", invoice.CurrencyCode);
    }

    [Fact]
    public void UpdateDetails_WithInvalidValues_ThrowsAndPreservesExistingDetails()
    {
        // Arrange
        var invoice = CreateInvoice();

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            invoice.UpdateDetails("INV-002", "Globex Corp", new DateOnly(2027, 1, 15), "US"));

        // Assert
        Assert.Equal("currencyCode", exception.ParamName);
        Assert.Equal("INV-001", invoice.InvoiceNumber);
        Assert.Equal("Acme Ltd", invoice.CustomerName);
        Assert.Equal(DefaultIssueDate, invoice.IssueDate);
        Assert.Equal("USD", invoice.CurrencyCode);
    }

    [Fact]
    public void PublicProperties_MatchRequiredDomainProperties()
    {
        // Arrange
        var expectedProperties = new[]
        {
            "CurrencyCode",
            "CustomerName",
            "Id",
            "InvoiceNumber",
            "IssueDate",
            "LineItems",
        };

        // Act
        var actualProperties = typeof(Invoice)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(property => property.Name)
            .Order()
            .ToArray();

        // Assert
        Assert.Equal(expectedProperties, actualProperties);
    }

    private static Invoice CreateInvoice() =>
        new(Guid.NewGuid(), "INV-001", "Acme Ltd", DefaultIssueDate, "USD");
}
