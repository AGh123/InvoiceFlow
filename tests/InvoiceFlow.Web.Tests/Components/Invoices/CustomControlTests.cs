using Bunit;
using InvoiceFlow.Application.Invoices;
using InvoiceFlow.Web.Components.Invoices;
using InvoiceFlow.Web.Components.Shared;
using Microsoft.AspNetCore.Components.Web;

namespace InvoiceFlow.Web.Tests.Components.Invoices;

public class CustomControlTests
{
    [Fact]
    public void DatePicker_ReflectsInitialValueAndSelectingDateCloses()
    {
        using var context = Context();
        DateOnly selected = default;
        var cut = context.Render<DatePicker>(parameters => parameters
            .Add(component => component.Id, "issue-date")
            .Add(component => component.LabelId, "issue-date-label")
            .Add(component => component.Value, new DateOnly(2026, 9, 25))
            .Add(component => component.ValueChanged, value => selected = value));

        Assert.Contains("Sep 25, 2026", cut.Find("#issue-date").TextContent);
        cut.Find("#issue-date").Click();
        Assert.Equal("true", cut.Find("#issue-date").GetAttribute("aria-expanded"));
        cut.Find("[aria-label='Thursday, September 24, 2026']").Click();

        Assert.Equal(new DateOnly(2026, 9, 24), selected);
        Assert.Equal("false", cut.Find("#issue-date").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void DatePicker_TodayActionSelectsToday()
    {
        using var context = Context();
        DateOnly selected = default;
        var cut = context.Render<DatePicker>(parameters => parameters
            .Add(component => component.Id, "issue-date")
            .Add(component => component.LabelId, "issue-date-label")
            .Add(component => component.Value, new DateOnly(2026, 9, 25))
            .Add(component => component.ValueChanged, value => selected = value));

        cut.Find("#issue-date").Click();
        cut.Find(".date-picker__today").Click();

        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), selected);
    }

    [Fact]
    public void InvalidCustomControls_ExposeValidationStateAndDescription()
    {
        using var context = Context();
        var date = context.Render<DatePicker>(parameters => parameters
            .Add(component => component.Id, "issue-date")
            .Add(component => component.LabelId, "issue-date-label")
            .Add(component => component.DescribedBy, "issue-date-error")
            .Add(component => component.Value, new DateOnly(2026, 9, 25))
            .Add(component => component.Invalid, true));
        var currency = context.Render<CurrencyCombobox>(parameters => parameters
            .Add(component => component.Invalid, true));

        Assert.Equal("true", date.Find("#issue-date").GetAttribute("aria-invalid"));
        Assert.Equal("issue-date-error", date.Find("#issue-date").GetAttribute("aria-describedby"));
        Assert.Equal("true", currency.Find("#currency-code").GetAttribute("aria-invalid"));
        Assert.Equal("currency-code-help currency-code-error",
            currency.Find("#currency-code").GetAttribute("aria-describedby"));
    }

    [Fact]
    public void DatePicker_ArrowMovesGridFocusAndEscapeCloses()
    {
        using var context = Context();
        var cut = context.Render<DatePicker>(parameters => parameters
            .Add(component => component.Id, "issue-date")
            .Add(component => component.LabelId, "issue-date-label")
            .Add(component => component.Value, new DateOnly(2026, 9, 25)));

        cut.Find("#issue-date").Click();
        cut.Find("[aria-label='Friday, September 25, 2026']")
            .KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal("0", cut.Find("[aria-label='Saturday, September 26, 2026']").GetAttribute("tabindex"));
        cut.Find(".date-picker__popover").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.Equal("false", cut.Find("#issue-date").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void CurrencyCombobox_SelectsSuggestionAndAcceptsArbitraryCode()
    {
        using var context = Context();
        string selected = "USD";
        var cut = context.Render<CurrencyCombobox>(parameters => parameters
            .Add(component => component.Value, selected)
            .Add(component => component.ValueChanged, value => selected = value));

        cut.Find("#currency-code").Input("eu");
        Assert.Contains("Euro", cut.Markup);
        cut.Find(".currency-combobox__option").Click();
        Assert.Equal("EUR", selected);
        Assert.Equal("false", cut.Find("#currency-code").GetAttribute("aria-expanded"));

        cut.Find("#currency-code").Input("chf");
        Assert.Equal("CHF", selected);
        Assert.Empty(cut.FindAll(".currency-combobox__option"));
    }

    [Fact]
    public void CurrencyCombobox_ArrowAndEnterSelectActiveSuggestion()
    {
        using var context = Context();
        string selected = "USD";
        var cut = context.Render<CurrencyCombobox>(parameters => parameters
            .Add(component => component.Value, selected)
            .Add(component => component.ValueChanged, value => selected = value));

        cut.Find("#currency-code").Input("eu");
        cut.Find("#currency-code").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.NotNull(cut.Find("#currency-code").GetAttribute("aria-activedescendant"));
        cut.Find("#currency-code").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("EUR", selected);
        Assert.Equal("false", cut.Find("#currency-code").GetAttribute("aria-expanded"));
    }

    [Theory]
    [InlineData("Newest first", InvoiceSortOption.Newest)]
    [InlineData("Oldest first", InvoiceSortOption.Oldest)]
    [InlineData("Customer A–Z", InvoiceSortOption.Customer)]
    [InlineData("Invoice number", InvoiceSortOption.InvoiceNumber)]
    public void SortControl_SelectsEachOption(string label, InvoiceSortOption expected)
    {
        using var context = Context();
        InvoiceSortOption selected = InvoiceSortOption.Newest;
        var cut = context.Render<InvoiceSortControl>(parameters => parameters
            .Add(component => component.Value, selected)
            .Add(component => component.ValueChanged, value => selected = value));

        cut.Find("#invoice-sort").Click();
        Assert.Equal("true", cut.Find("#invoice-sort").GetAttribute("aria-expanded"));
        cut.FindAll(".invoice-sort__option").Single(option => option.TextContent.Contains(label)).Click();

        Assert.Equal(expected, selected);
        Assert.Equal("false", cut.Find("#invoice-sort").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void SortControl_RepeatedArrowDownAdvancesThroughOptions()
    {
        using var context = Context();
        var cut = context.Render<InvoiceSortControl>();

        cut.Find("#invoice-sort").Click();
        cut.FindAll(".invoice-sort__option")[0].KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Equal("0", cut.FindAll(".invoice-sort__option")[1].GetAttribute("tabindex"));

        cut.FindAll(".invoice-sort__option")[1].KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Equal("0", cut.FindAll(".invoice-sort__option")[2].GetAttribute("tabindex"));
    }

    private static BunitContext Context()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }
}
