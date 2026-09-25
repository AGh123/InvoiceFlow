# InvoiceFlow

InvoiceFlow is a .NET 10 Blazor invoice-management application with SQLite persistence, layered architecture, a responsive UI, and automated tests.

## Features

- List, create, edit, and delete invoices.
- Add and remove invoice line items.
- Search invoices by invoice number or customer and sort the results.
- Calculate line gross amounts, discounts, line totals, invoice subtotals, and grand totals.
- Validate invoice details and nested line items.
- Use responsive desktop and mobile presentations.
- Persist data locally in SQLite.

## Technology

- .NET 10 and C#
- Blazor Web App with Interactive Server rendering
- Entity Framework Core and SQLite
- xUnit and bUnit
- GitHub Actions

## Architecture

The solution is split into four projects with clear responsibilities:

- **Domain** contains invoice entities, business rules, and calculations, with no dependency on persistence or UI code.
- **Application** defines invoice use cases through `InvoiceService`, DTOs, request models, the repository abstraction, and provider-neutral application exceptions.
- **Infrastructure** implements the repository with EF Core and SQLite, owns persistence configuration and migrations, and translates known provider-specific failures at the persistence boundary.
- **Web** contains the Blazor UI, mutable editor models, validation, and local UI state. Components perform persistence operations only through `InvoiceService`.

Tests are organized by the same boundaries: Domain unit tests, Application service tests, Infrastructure integration tests, and Web component/page tests.

## Domain model

`Invoice` and `InvoiceLineItem` each have exactly six public properties. Calculated values—including gross amount, discount amount, line total, subtotal, and grand total—are computed from the persisted data instead of being stored as additional Domain properties.

## State and data binding

The editor uses local Blazor component state, an `EditContext`, DataAnnotations, and mutable Web-specific editor models. Nested line-item validation is integrated into the same edit context. A narrowly scoped `InvoiceNotificationState` carries one-shot success feedback after navigation; no global state framework is used.

All persistence operations are delegated through `InvoiceService`, keeping UI state and data binding concerns out of the Domain and persistence layers.

## Validation and error handling

Editor validation provides immediate, field-level feedback, while Domain validation remains authoritative for business rules. A known SQLite uniqueness failure for an invoice number is translated into a provider-neutral Application exception and displayed by the Web UI as a field-level validation error. Unexpected persistence failures produce a generic user-safe message rather than exposing implementation details.

## Persistence

InvoiceFlow uses a local SQLite database at:

```text
src/InvoiceFlow.Web/App_Data/invoiceflow.db
```

The path is relative to the Web application's content root. The application creates the directory as needed and applies EF Core migrations during startup, so no external database server is required. Generated SQLite database files are excluded from source control.

## UX decisions

- Invoice lists use a table on desktop and cards on smaller screens while sharing the same data, filtering, sorting, and handlers.
- The invoice editor reflows for mobile use and provides clear saving, empty, error, and success states.
- Forms use meaningful labels, visible focus, keyboard-accessible controls, and an accessible native delete dialog with managed focus.
- Zero-line-item invoices are intentionally valid.
- Invoice numbers remain user-editable.
- Any valid three-letter currency code is supported.

## Getting started

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), then run the following commands from the repository root:

```shell
dotnet restore InvoiceFlow.slnx
dotnet build InvoiceFlow.slnx
dotnet run --project src/InvoiceFlow.Web/InvoiceFlow.Web.csproj
```

Open the HTTPS or HTTP address printed by ASP.NET in the terminal. No external database setup is needed.

## Using InvoiceFlow

1. Open **Invoices**.
2. Select **Create Invoice** to start a new invoice.
3. Enter the invoice number, customer, issue date, and currency details.
4. Use **Add Line Item** and **Remove** to manage line items as needed.
5. Select **Save Invoice**.
6. Select an invoice number in the list to edit an existing invoice.
7. Delete an existing invoice from its available actions when required.

## Running tests

Run the complete test suite from the repository root:

```shell
dotnet test InvoiceFlow.slnx
```

The solution currently contains 143 tests across four projects:

- `InvoiceFlow.Domain.Tests` covers entities, validation, and calculations.
- `InvoiceFlow.Application.Tests` covers service use cases and repository interactions.
- `InvoiceFlow.IntegrationTests` covers EF Core and SQLite persistence behavior.
- `InvoiceFlow.Web.Tests` uses bUnit to cover component and page behavior.

## CI

The GitHub Actions CI workflow restores dependencies, builds and tests the solution in Release configuration, publishes the Web application, and uploads the published output as the `invoiceflow-web` workflow artifact. It does not deploy to an external environment.

## Project structure

```text
src/
  InvoiceFlow.Domain/
  InvoiceFlow.Application/
  InvoiceFlow.Infrastructure/
  InvoiceFlow.Web/

tests/
  InvoiceFlow.Domain.Tests/
  InvoiceFlow.Application.Tests/
  InvoiceFlow.IntegrationTests/
  InvoiceFlow.Web.Tests/
```

## Design notes and trade-offs

- SQLite keeps the application portable and makes reviewer setup straightforward.
- Zero-line-item invoices are allowed by the established requirements.
- Mutable editor models stay in Web so UI binding concerns do not leak into Domain entities.
- Desktop tables and mobile cards use appropriate semantic markup while sharing state and behavior.
- No heavyweight UI or global state-management framework is required for the application's scope.
