using Bunit;
using InvoiceFlow.Web.Components.Invoices;

namespace InvoiceFlow.Web.Tests.Components.Invoices;

public class InvoiceSummaryTests
{
    [Fact]
    public void Totals_RenderSubtotalDiscountAndTotal()
    {
        using var context = new BunitContext();

        var cut = context.Render<InvoiceSummary>(parameters => parameters
            .Add(component => component.Subtotal, 125m)
            .Add(component => component.TotalDiscount, 25m)
            .Add(component => component.GrandTotal, 100m)
            .Add(component => component.CurrencyCode, "USD"));

        Assert.Contains("Subtotal", cut.Markup);
        Assert.Contains("$125.00", cut.Markup);
        Assert.Contains("-$25.00", cut.Markup);
        Assert.Contains("Total", cut.Markup);
        Assert.Contains("$100.00", cut.Markup);
    }

    [Fact]
    public void Discount_CanBeHiddenForZeroLineState()
    {
        using var context = new BunitContext();

        var cut = context.Render<InvoiceSummary>(parameters => parameters
            .Add(component => component.ShowDiscount, false));

        Assert.DoesNotContain("Discount", cut.Markup);
        Assert.Equal(2, cut.FindAll(".invoice-summary__row").Count);
        Assert.Equal(2, cut.Markup.Split("$0.00").Length - 1);
    }

    [Theory]
    [InlineData("USD", "$1,234.50")]
    [InlineData("EUR", "€1,234.50")]
    [InlineData("GBP", "£1,234.50")]
    [InlineData("JPY", "¥1,234.50")]
    [InlineData("CHF", "CHF 1,234.50")]
    public void Amounts_UseSupportedCurrencyFormatting(string currencyCode, string expected)
    {
        using var context = new BunitContext();

        var cut = context.Render<InvoiceSummary>(parameters => parameters
            .Add(component => component.Subtotal, 1234.5m)
            .Add(component => component.TotalDiscount, 0m)
            .Add(component => component.GrandTotal, 1234.5m)
            .Add(component => component.CurrencyCode, currencyCode));

        Assert.Contains(expected, cut.Markup);
        Assert.DoesNotContain("-$0.00", cut.Markup);
        Assert.DoesNotContain("-€0.00", cut.Markup);
        Assert.DoesNotContain("-£0.00", cut.Markup);
        Assert.DoesNotContain("-¥0.00", cut.Markup);
        Assert.DoesNotContain("-CHF 0.00", cut.Markup);
    }
}
