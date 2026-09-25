using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Web.Tests.TestDoubles;

internal static class BunitContextExtensions
{
    public static void AddWebValidation(this BunitContext context) =>
        context.Services.AddValidation();
}
