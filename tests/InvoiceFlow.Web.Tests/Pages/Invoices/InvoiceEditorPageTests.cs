using Bunit;
using InvoiceFlow.Application.Invoices;
using InvoiceFlow.Application.Invoices.Abstractions;
using InvoiceFlow.Application.Invoices.Exceptions;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Web.Components.Invoices;
using InvoiceFlow.Web.Components.Pages.Invoices;
using InvoiceFlow.Web.Tests.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Web.Tests.Pages.Invoices;

public class InvoiceEditorPageTests
{
    [Fact]
    public void NewRoute_InitializesNewEditorState()
    {
        using var context = CreateContext(out _);

        var cut = context.Render<InvoiceEditorPage>();

        Assert.Contains("New Invoice", cut.Markup);
        Assert.Contains("No line items yet", cut.Markup);
        Assert.Empty(cut.FindAll(".danger-zone"));
        Assert.Matches(@"^INV-\d{4}-\d{6}$", cut.Find("#invoice-number").GetAttribute("value")!);
    }

    [Fact]
    public void ExistingInvoice_LoadsIntoEditState()
    {
        using var context = CreateContext(out var repository);
        var invoice = Invoice();
        invoice.AddLineItem(Guid.NewGuid(), "Consulting", 2m, 50m, 10m);
        repository.Invoices.Add(invoice);

        var cut = context.Render<InvoiceEditorPage>(parameters => parameters
            .Add(component => component.Id, invoice.Id));

        Assert.Contains("Edit Invoice", cut.Markup);
        Assert.Equal("INV-100", cut.Find("#invoice-number").GetAttribute("value"));
        Assert.True(cut.Find("#invoice-number").HasAttribute("readonly"));
        Assert.Equal("Acme Ltd", cut.Find("#customer-name").GetAttribute("value"));
        Assert.Contains("Consulting", cut.Markup);
    }

    [Fact]
    public void UnknownInvoice_RendersNotFoundState()
    {
        using var context = CreateContext(out _);

        var cut = context.Render<InvoiceEditorPage>(parameters => parameters
            .Add(component => component.Id, Guid.NewGuid()));

        Assert.Contains("Invoice not found", cut.Markup);
        Assert.Contains("Back to Invoices", cut.Markup);
    }

    [Fact]
    public void Create_UsesRealServiceAndNavigatesToList()
    {
        using var context = CreateContext(out var repository);
        var cut = context.Render<InvoiceEditorPage>();
        FillRequiredFields(cut, "INV-NEW", "New Customer");

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal(1, repository.AddCallCount));
        Assert.Equal("INV-NEW", repository.LastAdded!.InvoiceNumber);
        Assert.Empty(repository.LastAdded.LineItems);
        cut.WaitForAssertion(() => Assert.EndsWith(
            "/invoices",
            context.Services.GetRequiredService<NavigationManager>().Uri));
    }

    [Fact]
    public void Update_UsesExistingIdAndDoesNotReloadAfterSave()
    {
        using var context = CreateContext(out var repository);
        var invoice = Invoice();
        repository.Invoices.Add(invoice);
        var cut = context.Render<InvoiceEditorPage>(parameters => parameters
            .Add(component => component.Id, invoice.Id));
        cut.Find("#customer-name").Change("Updated Customer");

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal(1, repository.UpdateCallCount));
        Assert.Equal(invoice.Id, repository.LastUpdated!.Id);
        Assert.Equal(2, repository.GetByIdCallCount);
        cut.WaitForAssertion(() => Assert.EndsWith(
            "/invoices",
            context.Services.GetRequiredService<NavigationManager>().Uri));
    }

    [Fact]
    public void DuplicateNumber_BecomesFieldErrorWithoutNavigation()
    {
        using var context = CreateContext(out var repository);
        repository.AddHandler = (invoice, _) => throw new DuplicateInvoiceNumberException(
            invoice.InvoiceNumber,
            new InvalidOperationException("UNIQUE constraint"));
        var cut = context.Render<InvoiceEditorPage>();
        FillRequiredFields(cut, "INV-DUP", "Acme Ltd");

        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
            Assert.Contains("An invoice with this number already exists.", cut.Markup));
        Assert.DoesNotContain("UNIQUE constraint", cut.Markup);
        Assert.Equal("INV-DUP", cut.Find("#invoice-number").GetAttribute("value"));
        Assert.EndsWith("/", context.Services.GetRequiredService<NavigationManager>().Uri);
        cut.Find(".final-actions .button-secondary").Click();
        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find(".discard-dialog").GetAttribute("data-open")));
    }

    [Fact]
    public void UnexpectedSaveFailure_PreservesFormAndShowsSafeError()
    {
        using var context = CreateContext(out var repository);
        repository.AddHandler = (_, _) => throw new InvalidOperationException("provider failure");
        var cut = context.Render<InvoiceEditorPage>();
        FillRequiredFields(cut, "INV-KEEP", "Still Here");

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("We couldn't save the invoice.", cut.Markup));
        Assert.Contains("Your changes are still here. Try again.", cut.Markup);
        Assert.DoesNotContain("provider failure", cut.Markup);
        Assert.Equal("INV-KEEP", cut.Find("#invoice-number").GetAttribute("value"));
        Assert.Equal("Still Here", cut.Find("#customer-name").GetAttribute("value"));
        cut.Find(".final-actions .button-secondary").Click();
        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find(".discard-dialog").GetAttribute("data-open")));
    }

    [Fact]
    public void NewInvoice_GeneratedNumberPersistsWithoutLoadingInvoiceList()
    {
        using var context = CreateContext(out var repository);
        var cut = context.Render<InvoiceEditorPage>();
        var shownNumber = cut.Find("#invoice-number").GetAttribute("value");
        cut.Find("#customer-name").Change("Acme Ltd");

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal(shownNumber, repository.LastAdded?.InvoiceNumber));
        Assert.Equal(0, repository.GetAllCallCount);
        Assert.Single(repository.Invoices);
    }

    [Fact]
    public void CleanNewEditor_NavigatesWithoutDiscardDialog()
    {
        using var context = CreateContext(out _);
        var cut = context.Render<InvoiceEditorPage>();
        cut.Find(".final-actions .button-secondary").Click();

        Assert.EndsWith("/invoices", context.Services.GetRequiredService<NavigationManager>().Uri);
        Assert.Equal("false", cut.Find(".discard-dialog").GetAttribute("data-open"));
    }

    [Fact]
    public void DirtyEditor_AsksBeforeNavigationAndKeepEditingPreservesInput()
    {
        using var context = CreateContext(out _);
        var cut = context.Render<InvoiceEditorPage>();
        cut.Find("#customer-name").Input("Changed customer");
        cut.Find(".final-actions .button-secondary").Click();

        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find(".discard-dialog").GetAttribute("data-open")));
        Assert.Equal("Changed customer", cut.Find("#customer-name").GetAttribute("value"));
        cut.Find(".discard-actions .button-secondary").Click();
        Assert.Equal("false", cut.Find(".discard-dialog").GetAttribute("data-open"));
        Assert.DoesNotContain("/invoices", context.Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void DirtyEditor_DiscardAllowsNavigation()
    {
        using var context = CreateContext(out _);
        var cut = context.Render<InvoiceEditorPage>();
        cut.Find(".empty-add-button").Click();
        cut.Find(".final-actions .button-secondary").Click();

        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find(".discard-dialog").GetAttribute("data-open")));
        cut.Find(".discard-actions .button-danger").Click();
        cut.WaitForAssertion(() => Assert.EndsWith(
            "/invoices", context.Services.GetRequiredService<NavigationManager>().Uri));
    }

    [Fact]
    public void ExistingEditor_StartsCleanAndChangingDatePrompts()
    {
        using var context = CreateContext(out var repository);
        var invoice = Invoice();
        repository.Invoices.Add(invoice);
        var cut = context.Render<InvoiceEditorPage>(parameters => parameters.Add(component => component.Id, invoice.Id));
        Assert.Equal("false", cut.Find(".discard-dialog").GetAttribute("data-open"));

        cut.Find("#issue-date").Click();
        cut.Find("[aria-label='Thursday, September 24, 2026']").Click();
        cut.Find(".final-actions .button-secondary").Click();

        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find(".discard-dialog").GetAttribute("data-open")));
    }

    [Fact]
    public void LoadedExistingEditor_CanLeaveCleanly()
    {
        using var context = CreateContext(out var repository);
        var invoice = Invoice();
        repository.Invoices.Add(invoice);
        var cut = context.Render<InvoiceEditorPage>(parameters => parameters.Add(component => component.Id, invoice.Id));

        cut.Find(".final-actions .button-secondary").Click();

        Assert.EndsWith("/invoices", context.Services.GetRequiredService<NavigationManager>().Uri);
        Assert.Equal("false", cut.Find(".discard-dialog").GetAttribute("data-open"));
    }

    [Fact]
    public void CurrencyChangeAndInvalidSubmitRemainDirty()
    {
        using var context = CreateContext(out _);
        var cut = context.Render<InvoiceEditorPage>();
        cut.Find("#currency-code").Input("chf");
        cut.Find("form").Submit();
        Assert.Contains("Customer name is required.", cut.Markup);

        cut.Find(".final-actions .button-secondary").Click();

        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find(".discard-dialog").GetAttribute("data-open")));
        Assert.Equal("CHF", cut.Find("#currency-code").GetAttribute("value"));
    }

    [Fact]
    public void EditingAndRemovingExistingLineItemCountAsDirty()
    {
        using var context = CreateContext(out var repository);
        var invoice = Invoice();
        invoice.AddLineItem(Guid.NewGuid(), "Original", 1m, 10m, 0m);
        repository.Invoices.Add(invoice);
        var cut = context.Render<InvoiceEditorPage>(parameters => parameters.Add(component => component.Id, invoice.Id));
        cut.Find(".description-field input").Input("Changed");
        cut.Find(".final-actions .button-secondary").Click();
        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find(".discard-dialog").GetAttribute("data-open")));
        cut.Find(".discard-actions .button-secondary").Click();

        cut.Find(".remove-item-button").Click();
        cut.Find(".final-actions .button-secondary").Click();
        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find(".discard-dialog").GetAttribute("data-open")));
    }

    [Fact]
    public void RemovingExistingLineItemAlonePrompts()
    {
        using var context = CreateContext(out var repository);
        var invoice = Invoice();
        invoice.AddLineItem(Guid.NewGuid(), "Existing", 1m, 10m, 0m);
        repository.Invoices.Add(invoice);
        var cut = context.Render<InvoiceEditorPage>(parameters => parameters.Add(component => component.Id, invoice.Id));

        cut.Find(".remove-item-button").Click();
        cut.Find(".final-actions .button-secondary").Click();

        cut.WaitForAssertion(() => Assert.Equal("true", cut.Find(".discard-dialog").GetAttribute("data-open")));
    }

    private static BunitContext CreateContext(out FakeInvoiceRepository repository)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.AddWebValidation();
        repository = new FakeInvoiceRepository();
        context.Services.AddSingleton<IInvoiceRepository>(repository);
        context.Services.AddSingleton<InvoiceService>();
        context.Services.AddSingleton<InvoiceNotificationState>();
        return context;
    }

    private static void FillRequiredFields(IRenderedComponent<InvoiceEditorPage> cut, string number, string customer)
    {
        cut.Find("#invoice-number").Change(number);
        cut.Find("#customer-name").Change(customer);
    }

    private static Invoice Invoice() =>
        new(Guid.NewGuid(), "INV-100", "Acme Ltd", new DateOnly(2026, 9, 25), "USD");
}
