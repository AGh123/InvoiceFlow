using Bunit;
using InvoiceFlow.Application.Invoices;
using InvoiceFlow.Application.Invoices.Abstractions;
using InvoiceFlow.Application.Invoices.Dtos;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Web.Components.Invoices;
using InvoiceFlow.Web.Components.Pages.Invoices;
using InvoiceFlow.Web.Tests.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Web.Tests.Components.Invoices;

public class InvoiceListInteractionTests
{
    [Theory]
    [InlineData("Newest first", "INV-B,INV-C,INV-A")]
    [InlineData("Oldest first", "INV-A,INV-C,INV-B")]
    [InlineData("Customer A–Z", "INV-C,INV-A,INV-B")]
    [InlineData("Invoice number", "INV-A,INV-B,INV-C")]
    public void SortMenu_PreservesExpectedOrdering(string option, string expected)
    {
        using var context = Context(out var repository);
        foreach (var summary in Invoices())
        {
            repository.Invoices.Add(new Invoice(summary.Id, summary.InvoiceNumber,
                summary.CustomerName, summary.IssueDate, summary.CurrencyCode));
        }
        context.Services.AddSingleton<InvoiceNotificationState>();
        var cut = context.Render<InvoiceListPage>();
        Assert.Equal("Invoice list pagination", cut.Find("nav.invoice-list__pagination").GetAttribute("aria-label"));
        cut.Find("#invoice-sort").Click();
        cut.FindAll(".invoice-sort__option").Single(item => item.TextContent.Contains(option)).Click();

        var actual = string.Join(',', cut.FindAll(".invoice-list__desktop tbody tr .invoice-list__link")
            .Select(link => link.TextContent.Trim()));
        Assert.Equal(expected, actual);
        Assert.Equal(option, cut.Find("#invoice-sort span").TextContent);
        Assert.Single(cut.FindAll(".invoice-list__desktop table"));
        Assert.Single(cut.FindAll(".invoice-list__mobile"));
        Assert.Equal(3, cut.FindAll(".invoice-list__mobile .invoice-list__card").Count);
    }

    [Fact]
    public void InvoiceActions_OnlyOneMenuOpensAndContainsOpenAndDelete()
    {
        using var context = Context(out _);
        var module = context.JSInterop.SetupModule("./Components/Invoices/InvoiceList/InvoiceActionsMenu/InvoiceActionsMenu.razor.js");
        module.Mode = JSRuntimeMode.Loose;
        var cut = context.Render<InvoiceList>(parameters => parameters.Add(
            component => component.Result, Result(Invoices())));
        var triggers = cut.FindAll(".invoice-list__desktop .invoice-actions__trigger");

        triggers[0].Click();
        Assert.Single(cut.FindAll(".invoice-list__desktop .invoice-actions__menu"));
        Assert.Equal(triggers[0].Id,
            cut.Find(".invoice-list__desktop .invoice-actions__menu").GetAttribute("aria-labelledby"));
        Assert.Equal(triggers.Count, triggers.Select(trigger => trigger.Id).Distinct().Count());
        Assert.Equal("true", cut.FindAll(".invoice-list__desktop .invoice-actions__trigger")[0].GetAttribute("aria-expanded"));
        Assert.Contains("Open invoice", cut.Find(".invoice-list__desktop .invoice-actions__menu").TextContent);
        Assert.StartsWith("/invoices/", cut.Find(".invoice-list__desktop .invoice-actions__menu a").GetAttribute("href"));
        Assert.Contains("Delete", cut.Find(".invoice-list__desktop .invoice-actions__menu").TextContent);

        triggers[1].Click();
        Assert.Single(cut.FindAll(".invoice-list__desktop .invoice-actions__menu"));
        Assert.Equal("false", cut.FindAll(".invoice-list__desktop .invoice-actions__trigger")[0].GetAttribute("aria-expanded"));
        Assert.Equal("true", cut.FindAll(".invoice-list__desktop .invoice-actions__trigger")[1].GetAttribute("aria-expanded"));
        module.VerifyInvoke("unwatchOutside");
    }

    [Fact]
    public void InvoiceActions_ExternalOpenFalseUnwatchesOutsideListener()
    {
        using var context = Context(out _);
        var module = context.JSInterop.SetupModule("./Components/Invoices/InvoiceList/InvoiceActionsMenu/InvoiceActionsMenu.razor.js");
        module.Mode = JSRuntimeMode.Loose;
        var invoice = Invoices()[0];
        var cut = context.Render<InvoiceActionsMenu>(parameters => parameters
            .Add(component => component.Invoice, invoice)
            .Add(component => component.Open, true));

        module.VerifyInvoke("watchOutside");
        cut.Render(parameters => parameters
            .Add(component => component.Invoice, invoice)
            .Add(component => component.Open, false));

        module.VerifyInvoke("unwatchOutside");
        Assert.Equal("false", cut.Find(".invoice-actions__trigger").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void DeleteMenuAction_OpensConfirmationAndDeletesSelectedInvoice()
    {
        using var context = Context(out var repository);
        var dialogModule = context.JSInterop.SetupModule(
            "./js/dialog.js");
        dialogModule.Mode = JSRuntimeMode.Loose;
        var invoice = Invoices()[0];
        repository.Invoices.Add(new Invoice(invoice.Id, invoice.InvoiceNumber,
            invoice.CustomerName, invoice.IssueDate, invoice.CurrencyCode));
        context.Services.AddSingleton<InvoiceNotificationState>();
        var cut = context.Render<InvoiceListPage>();

        cut.Find(".invoice-list__desktop .invoice-actions__trigger").Click();
        cut.Find(".invoice-list__desktop .invoice-actions__menu button").Click();
        dialogModule.VerifyInvoke("showDialog");
        Assert.Empty(cut.FindAll(".invoice-actions__menu"));
        Assert.Contains("Delete invoice?", cut.Markup);
        Assert.Contains(invoice.InvoiceNumber, cut.Find(".invoice-delete-dialog").TextContent);
        Assert.Contains(invoice.CustomerName, cut.Find(".invoice-delete-dialog").TextContent);

        cut.Find(".invoice-delete-dialog .button--secondary").Click();
        dialogModule.VerifyInvoke("closeDialog");
        Assert.Single(repository.Invoices);
        Assert.Single(cut.FindAll(".invoice-list__desktop .invoice-list__link"));

        cut.Find(".invoice-list__desktop .invoice-actions__trigger").Click();
        cut.Find(".invoice-list__desktop .invoice-actions__menu button").Click();
        cut.Find(".invoice-delete-dialog .button--danger").Click();

        cut.WaitForAssertion(() => Assert.Contains("deleted successfully", cut.Markup));
        Assert.Empty(repository.Invoices);
        Assert.Empty(cut.FindAll(".invoice-list__desktop .invoice-list__link"));
    }

    [Fact]
    public void CleanEditorCancel_ThenListMenuDelete_CompletesWithoutStaleComponentCallbacks()
    {
        using var context = Context(out var repository);
        context.AddWebValidation();
        context.Services.AddSingleton<InvoiceNotificationState>();
        var invoice = new Invoice(Guid.NewGuid(), "INV-RETURN", "Return customer",
            new DateOnly(2026, 9, 25), "USD");
        repository.Invoices.Add(invoice);
        var editor = context.Render<InvoiceEditorPage>(parameters => parameters
            .Add(component => component.Id, invoice.Id));

        editor.Find(".invoice-editor__final-actions .button--secondary").Click();
        Assert.EndsWith("/invoices", context.Services.GetRequiredService<NavigationManager>().Uri);
        editor.Dispose();

        var summary = new InvoiceSummaryDto(invoice.Id, invoice.InvoiceNumber,
            invoice.CustomerName, invoice.IssueDate, invoice.CurrencyCode, 0m);
        var list = context.Render<InvoiceList>(parameters => parameters
            .Add(component => component.Result, Result([summary])));
        list.Find(".invoice-list__desktop .invoice-actions__trigger").Click();
        list.Find(".invoice-list__desktop .invoice-actions__menu button").Click();
        list.Find(".invoice-delete-dialog .button--danger").Click();

        list.WaitForAssertion(() => Assert.Empty(repository.Invoices));
    }

    private static BunitContext Context(out FakeInvoiceRepository repository)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        repository = new FakeInvoiceRepository();
        context.Services.AddSingleton<IInvoiceRepository>(repository);
        context.Services.AddSingleton<InvoiceService>();
        return context;
    }

    private static InvoiceSummaryDto[] Invoices() =>
    [
        new(Guid.NewGuid(), "INV-A", "Beta", new DateOnly(2026, 1, 1), "USD", 1m),
        new(Guid.NewGuid(), "INV-B", "Zulu", new DateOnly(2026, 3, 1), "USD", 2m),
        new(Guid.NewGuid(), "INV-C", "Alpha", new DateOnly(2026, 2, 1), "CHF", 3m),
    ];

    private static InvoiceListResultDto Result(IReadOnlyList<InvoiceSummaryDto> invoices) =>
        new(invoices, invoices.Count, invoices.Count,
            invoices.Count == 0 ? null : invoices.Max(invoice => invoice.IssueDate), 1);
}
