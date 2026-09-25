using InvoiceFlow.Application.Invoices;
using InvoiceFlow.Infrastructure;
using InvoiceFlow.Infrastructure.Persistence;
using InvoiceFlow.Web.Components;
using InvoiceFlow.Web.Components.Invoices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddValidation();

var databasePath = Path.Combine(
    builder.Environment.ContentRootPath,
    "App_Data",
    "invoiceflow.db");

builder.Services.AddInfrastructure(databasePath);
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<InvoiceNotificationState>();

var app = builder.Build();

await app.Services.ApplyDatabaseMigrationsAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
