using Bunit;
using InvoiceFlow.Application.Invoices;
using InvoiceFlow.Application.Invoices.Abstractions;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Web.Components.Invoices;
using InvoiceFlow.Web.Components.Pages.Invoices;
using InvoiceFlow.Web.Components.Shared;
using InvoiceFlow.Web.Tests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Web.Tests.Pages.Invoices;

public class InvoiceListPageTests
{
    [Fact]
    public void EmptyResult_RendersEmptyState()
    {
        using var context = CreateContext(out _, out _);

        var cut = context.Render<InvoiceListPage>();

        Assert.Contains("No invoices yet", cut.Markup);
        Assert.Contains("Create Invoice", cut.Markup);
    }

    [Fact]
    public void PopulatedResult_RendersInvoices()
    {
        using var context = CreateContext(out var repository, out _);
        repository.Invoices.Add(Invoice());

        var cut = context.Render<InvoiceListPage>();

        Assert.Contains("INV-100", cut.Markup);
        Assert.Contains("Acme Ltd", cut.Markup);
        Assert.Contains("USD 125.00", cut.Markup);
    }

    [Fact]
    public void Search_DebouncesDatabaseQueryAndKeepsSummaryCounts()
    {
        using var context = CreateContext(out var repository, out _);
        repository.Invoices.Add(Invoice());
        repository.Invoices.Add(new Invoice(Guid.NewGuid(), "INV-200", "Globex",
            new DateOnly(2026, 9, 24), "EUR"));
        var cut = context.Render<InvoiceListPage>();
        Assert.Equal(1, repository.GetPageCallCount);

        cut.Find("#invoice-search").Input("Globex");
        Assert.Equal(1, repository.GetPageCallCount);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, repository.GetPageCallCount);
            Assert.Contains("Showing 1–1 of 1 invoices", cut.Markup);
            Assert.Contains("Total Invoices", cut.Markup);
            Assert.Contains("INV-200", cut.Markup);
            Assert.DoesNotContain("INV-100", cut.Markup);
        });
    }

    [Fact]
    public void Pagination_QueriesNextPageAndShowsFilteredCount()
    {
        using var context = CreateContext(out var repository, out _);
        for (var number = 1; number <= 11; number++)
        {
            repository.Invoices.Add(new Invoice(Guid.NewGuid(), $"INV-{number:000}",
                "Acme", new DateOnly(2026, 1, number), "USD"));
        }
        var cut = context.Render<InvoiceListPage>();
        Assert.Equal(10, cut.FindAll(".invoice-list__desktop tbody tr").Count);

        cut.Find(".invoice-list__pagination-buttons button:last-child").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, repository.GetPageCallCount);
            Assert.Single(cut.FindAll(".invoice-list__desktop tbody tr"));
            Assert.Contains("Showing 11–11 of 11 invoices", cut.Markup);
            Assert.Contains("INV-001", cut.Markup);
        });
    }

    [Fact]
    public void DeletingFinalItemOnLastPage_ReturnsToPreviousPage()
    {
        using var context = CreateContext(out var repository, out _);
        for (var number = 1; number <= 11; number++)
        {
            repository.Invoices.Add(new Invoice(Guid.NewGuid(), $"INV-{number:000}",
                "Acme", new DateOnly(2026, 1, number), "USD"));
        }
        var cut = context.Render<InvoiceListPage>();
        cut.Find(".invoice-list__pagination-buttons button:last-child").Click();
        cut.Find(".invoice-list__desktop .invoice-actions__trigger").Click();
        cut.Find(".invoice-list__desktop .invoice-actions__menu button").Click();
        cut.Find(".invoice-delete-dialog .button--danger").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(10, repository.Invoices.Count);
            Assert.Equal(10, cut.FindAll(".invoice-list__desktop tbody tr").Count);
            Assert.Contains("Showing 1–10 of 10 invoices", cut.Markup);
            Assert.True(cut.Find(".invoice-list__pagination-buttons button:last-child").HasAttribute("disabled"));
        });
    }

    [Fact]
    public void Notification_IsConsumedAndDisplayedOnlyOnce()
    {
        using var context = CreateContext(out _, out var notificationState);
        notificationState.Publish("Invoice INV-100 saved successfully");

        var first = context.Render<InvoiceListPage>();
        Assert.Contains("Invoice INV-100 saved successfully", first.Markup);
        Assert.Equal(3000, first.FindComponent<StatusMessage>().Instance.AutoDismissMilliseconds);
        first.Dispose();

        var second = context.Render<InvoiceListPage>();
        Assert.DoesNotContain("Invoice INV-100 saved successfully", second.Markup);
    }

    [Fact]
    public void DeletingFromList_ShowsFloatingSuccessMessage()
    {
        using var context = CreateContext(out var repository, out _);
        repository.Invoices.Add(Invoice());
        var cut = context.Render<InvoiceListPage>();

        cut.Find(".invoice-list__desktop .invoice-actions__trigger").Click();
        cut.Find(".invoice-list__desktop .invoice-actions__menu button").Click();
        cut.Find(".invoice-delete-dialog .button--danger").Click();

        cut.WaitForAssertion(() =>
        {
            var message = cut.FindComponent<StatusMessage>();
            Assert.Equal("Invoice INV-100 deleted successfully", message.Instance.Message);
            Assert.True(message.Instance.Floating);
            Assert.Equal(3000, message.Instance.AutoDismissMilliseconds);
            Assert.Empty(repository.Invoices);
        });
    }

    [Fact]
    public void ServiceFailure_RendersSafeRetryState()
    {
        using var context = CreateContext(out var repository, out _);
        repository.GetPageHandler = (_, _) => throw new InvalidOperationException("database details");

        var cut = context.Render<InvoiceListPage>();

        Assert.Contains("We couldn't load your invoices.", cut.Markup);
        Assert.Contains("Try Again", cut.Markup);
        Assert.DoesNotContain("database details", cut.Markup);
    }

    private static BunitContext CreateContext(
        out FakeInvoiceRepository repository,
        out InvoiceNotificationState notificationState)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        repository = new FakeInvoiceRepository();
        notificationState = new InvoiceNotificationState();
        context.Services.AddSingleton<IInvoiceRepository>(repository);
        context.Services.AddSingleton<InvoiceService>();
        context.Services.AddSingleton(notificationState);
        return context;
    }

    private static Invoice Invoice()
    {
        var invoice = new Invoice(
            Guid.NewGuid(),
            "INV-100",
            "Acme Ltd",
            new DateOnly(2026, 9, 25),
            "USD");
        invoice.AddLineItem(Guid.NewGuid(), "Consulting", 1m, 125m, 0m);
        return invoice;
    }
}
