# AGENTS.md

## Purpose

This repository should read like a deliberately designed .NET 10 and Blazor application maintained by experienced engineers. Changes must improve correctness, clarity, maintainability, accessibility, testability, and performance without introducing unnecessary abstraction or framework ceremony.

Prefer simple, explicit, idiomatic .NET and Blazor code. Do not imitate Angular, React, or another framework when Blazor already has a better native pattern.

## Working Rules

- Inspect the existing implementation before changing it.
- Preserve established product behavior unless the task explicitly changes a requirement.
- Make the smallest coherent change that solves the problem completely.
- Do not add abstractions, packages, design patterns, layers, or helper classes unless they remove real duplication, improve a boundary, or make a rule explicit.
- Do not add MediatR, AutoMapper, a generic repository, a global state framework, or a separate REST API unless a future requirement genuinely needs them.
- Do not commit or push unless explicitly requested.
- Do not leave temporary files, generated artifacts, commented-out code, dead code, unused assets, or speculative TODOs.
- Keep the solution buildable with zero warnings.
- Run the relevant tests after every meaningful refactor and the full suite before declaring the task complete.

## Architecture

The intended dependency direction is:

```text
InvoiceFlow.Domain
    ↑
InvoiceFlow.Application
    ↑
InvoiceFlow.Infrastructure
    ↑
InvoiceFlow.Web
```

More precisely:

- `Domain` contains business entities, invariants, calculations, and domain rules. It must not depend on EF Core, Blazor, Infrastructure, or Web.
- `Application` contains use-case orchestration, DTOs, requests, ports/abstractions, and provider-neutral application exceptions. It may depend on Domain, but not on Infrastructure or Web.
- `Infrastructure` implements Application abstractions and owns EF Core, SQLite, persistence configuration, migrations, and provider-specific translation.
- `Web` is the composition/UI layer. It owns Blazor pages/components, editor/view models, presentation formatting, local UI state, and browser interaction.

Do not bypass these boundaries. Web components must not access `DbContext` directly. Provider-specific exceptions and SQL details must not leak beyond Infrastructure.

## Domain Design

- Domain rules are authoritative. UI validation may improve UX, but it must not be the only enforcement of a business invariant.
- Keep entity state protected with private setters and mutation methods where appropriate.
- Keep aggregate collections protected from arbitrary external mutation.
- Enforce stable business invariants in Domain rather than only in UI.
- If an identifier or business key is immutable after creation, model that immutability explicitly instead of relying on the UI to keep it unchanged.
- Centralize reusable domain limits and rule constants when the same rule is needed by Domain, EF configuration, and Web validation.
- Avoid primitive obsession only when a value has meaningful domain behavior. Do not create value objects merely for appearance.
- Calculated values should remain derived when persistence is unnecessary.
- Preserve zero-line-item invoices as valid unless requirements change.

## Application Layer

- Use Application services for use-case orchestration, not UI concerns.
- Avoid god services. Split responsibilities only when a class has clearly accumulated unrelated behavior.
- Avoid duplicating aggregate reconciliation between Application and Infrastructure. One layer should own each piece of behavior.
- Keep mapping logic small and explicit. Introduce a mapper/helper only when it removes meaningful repetition.
- Use typed values for closed sets. Prefer enums or dedicated types over magic strings when the valid set is finite and controlled by the application.

Examples that should be strongly typed:

- invoice sort options,
- message/status variants,
- other internal mode/state values with a fixed set of choices.

Examples that should normally remain strings:

- arbitrary customer names,
- invoice numbers,
- ISO-style currency codes when the application intentionally accepts any valid three-letter code,
- user-entered search text,
- external/provider values whose set is not owned by the application.

Do not convert strings to enums mechanically. Use an enum only when it represents a real closed set.

## Infrastructure and EF Core

- Continue using `IDbContextFactory<InvoiceFlowDbContext>` for Blazor Interactive Server data access.
- Keep DbContext instances short-lived and scoped to an operation.
- Use `AsNoTracking()` for read-only queries.
- Avoid loading full aggregates when a projection is sufficient.
- Avoid multiple database reads for a single logical operation when one tracked operation can do the job safely.
- Keep database uniqueness constraints as the final protection against collisions.
- Translate only known provider-specific failures into provider-neutral Application exceptions.
- Keep EF configuration explicit and aligned with Domain validation.
- Treat migrations as source-controlled application assets.
- Runtime migration on startup is acceptable for this assignment/local setup; production-oriented changes should prefer a deliberate migration deployment strategy.
- Do not write raw SQL when EF Core provides a clear and correct alternative. Raw SQL is acceptable for narrowly scoped SQLite behavior that EF does not model well.

## Blazor Components

Use idiomatic Blazor rather than copying Angular structure.

### When to keep a single `.razor` file

Keep markup and `@code` together when the component is small, cohesive, and easy to understand.

### When to use `.razor.cs`

Move logic to a code-behind partial class when a component has substantial:

- lifecycle handling,
- validation orchestration,
- state-machine behavior,
- dependency injection,
- async operations,
- JS interop,
- navigation logic,
- or enough C# that the markup becomes hard to read.

Do not split trivial components just to create more files.

### Component folders

Use a component-specific folder when a component has multiple companion files or meaningful complexity, for example:

```text
InvoiceEditor/
├── InvoiceEditor.razor
├── InvoiceEditor.razor.cs
└── InvoiceEditor.razor.css
```

or:

```text
CurrencyCombobox/
├── CurrencyCombobox.razor
├── CurrencyCombobox.razor.cs
├── CurrencyCombobox.razor.css
└── CurrencyCombobox.razor.js
```

Simple one-file components do not need a folder solely for consistency.

Prefer feature-oriented folders and clear names over generic catch-all folders such as `Components/Components`.

Keep editor/view models outside component files in an appropriate `Models` folder. Keep state classes in a `State` folder when that improves discoverability.

## JavaScript Interop

- Use JavaScript only for browser/DOM behavior that cannot be expressed cleanly in Blazor/C#.
- Good JS interop use cases include native dialog APIs, focus management, DOM measurement, outside-click/focus detection, native selection, and preventing browser-default keyboard behavior.
- Do not convert legitimate DOM logic to C# merely to eliminate JavaScript.
- Prefer ES modules and collocated `.razor.js` files for component-specific behavior.
- Shared JS modules must contain genuinely generic browser primitives and must not know invoice-specific selectors or component-specific class names.
- A shared component must never import JavaScript from a feature-specific component.
- Dispose JS modules and event handlers correctly and handle disconnected Blazor circuits safely.

## CSS and BEM

All authored application CSS should follow BEM unless a framework-generated class or tiny global utility makes BEM inappropriate.

Use:

```text
.block
.block__element
.block--modifier
.block__element--modifier
```

Examples:

```css
.invoice-editor {}
.invoice-editor__field {}
.invoice-editor__actions {}
.invoice-editor--saving {}

.currency-combobox {}
.currency-combobox__input {}
.currency-combobox__option {}
.currency-combobox__option--active {}

.button {}
.button--primary {}
.button--secondary {}
.button--danger {}
```

Framework/global exceptions may include classes such as:

- `.validation-message`,
- `.invalid`,
- `.visually-hidden`.

Additional CSS rules:

- Prefer CSS isolation with `.razor.css` for component-specific styling.
- Keep global CSS limited to resets, tokens, true utilities, typography, and reusable global primitives.
- Do not duplicate the same control styling across multiple components. Extract a reusable global block or shared component style when the visual contract is genuinely identical.
- Use design tokens instead of repeating literal colors, radii, spacing, or shadows.
- Keep responsive behavior intentional and avoid arbitrary one-off breakpoints.
- Preserve accessible focus states and reduced-motion behavior.
- Do not use overly specific selectors when a clear BEM class can express the intent.

## Naming and Types

- Use names that describe intent, not implementation accidents.
- Prefer enums for finite internal states instead of string literals.
- Prefer `const`/`static readonly` values for repeated internal constants.
- Avoid duplicated magic strings across files.
- Avoid boolean parameters when an enum would communicate multiple states more clearly.
- Do not introduce enums for open-ended values such as arbitrary ISO currency codes.
- Keep namespaces aligned with logical ownership, but avoid excessively deep nesting created only by folder structure.

## Performance

Optimize real work, not microscopic syntax.

- Avoid repeated serialization or expensive derived-state computation during rendering when an explicit state flag is sufficient.
- Avoid repeatedly sorting/filtering/materializing the same collection in one render path.
- Use server-side filtering/paging/projection when data volume can grow enough to justify it.
- Do not load line-item aggregates for an invoice-list view if only summary data is required.
- Be mindful that `oninput` events in Interactive Server travel over the Blazor circuit. Use them where immediate feedback is valuable; debounce or prefer lower-frequency events when it is not.
- Use `@key` when component identity matters.
- Avoid unnecessary `StateHasChanged` calls.
- Measure before introducing advanced optimization mechanisms.

## Validation and Error Handling

- Domain validation protects invariants.
- Web validation provides immediate field-level UX.
- Persistence constraints provide final storage guarantees.
- Keep provider-specific error parsing in Infrastructure.
- Show user-safe messages in Web; do not expose provider/SQL/stack-trace details.
- Preserve user-entered form state after recoverable save failures.
- Log unexpected exceptions with useful structured context.
- Do not swallow exceptions silently except narrowly understood disconnect/cancellation cases.

## Accessibility

Every UI change must preserve or improve accessibility.

- Use native semantic HTML where possible.
- Keep labels associated with form controls.
- Maintain `aria-invalid`, `aria-describedby`, and live-region behavior where relevant.
- Interactive controls must be keyboard accessible.
- Manage focus for dialogs, validation errors, menus, comboboxes, and date pickers.
- Keep visible focus indicators.
- Use sufficient touch targets on mobile.
- Decorative images/icons must not create redundant screen-reader output.
- Do not rely on color alone to communicate state.

## Tests

Maintain tests at the same architectural boundaries as production code:

- Domain tests for invariants and calculations.
- Application tests for use cases and repository interaction.
- Integration tests for EF Core/SQLite behavior and provider translation.
- Web/bUnit tests for component/page state and rendering.
- A small real-browser E2E suite should cover behavior that bUnit cannot validate, especially JavaScript, focus, dialogs, outside clicks, and critical create/edit/delete flows.

Testing rules:

- Add or update tests with every behavior change.
- Refactors that should not change behavior must keep existing tests green.
- Do not weaken tests simply to make a refactor pass.
- Avoid duplicating large fake implementations when a shared test double can remain clear and isolated.
- Keep test names behavior-oriented.

## Code Quality

- Keep nullable reference types enabled.
- Keep implicit usings and deterministic builds unless a real need changes them.
- Prefer file-scoped namespaces.
- Follow `.editorconfig`.
- Use analyzers and code style enforcement deliberately; do not enable noisy rules blindly.
- Keep the build at zero warnings.
- Remove unused usings, methods, fields, files, packages, assets, CSS selectors, and JS exports.
- Prefer guard clauses for invalid input and early exits.
- Keep methods small enough to express one coherent operation.
- Extract a method when it names a concept or removes real duplication, not just to reduce line count.
- Avoid comments that restate the code. Comment only important rationale, framework quirks, or non-obvious constraints.

## Reuse and Duplication

Before adding code, search for an existing implementation.

Remove duplication when two places implement the same business rule, formatting rule, persistence operation, browser primitive, or visual contract.

Do not create a shared abstraction merely because two snippets look superficially similar. Share only concepts that are semantically the same.

Examples worth sharing:

- currency display formatting,
- shared dialog browser behavior,
- repeated input visual styling,
- domain rule constants,
- fixed sort/state enums.

Examples that can remain separate:

- two components with different behavior that happen to use similar markup,
- feature-specific state that is unlikely to be reused,
- tiny one-line helpers whose abstraction would be harder to understand than duplication.

## Repository Hygiene

- Keep source under `src/` and tests under `tests/`.
- Keep component, model, state, persistence, and test concerns in discoverable folders.
- Remove exact duplicate static assets when they do not intentionally represent separate semantic assets.
- Do not commit local SQLite database files, publish output, `bin/`, `obj/`, coverage output, or temporary artifacts.
- Keep README architecture and behavior descriptions synchronized with the implementation.
- Keep CI representative of the supported build/test/publish workflow.

## Definition of Done

Before declaring a task complete:

1. Re-read the changed code for duplication, magic strings, dead code, naming issues, and boundary violations.
2. Confirm closed internal states use suitable strong types rather than repeated strings.
3. Confirm Domain and EF validation remain aligned.
4. Confirm Razor/JS/CSS ownership is clear and component organization is appropriate to complexity.
5. Confirm CSS follows BEM for authored application classes.
6. Confirm accessibility and responsive behavior were preserved.
7. Run formatting/static checks used by the repository.
8. Run Release build with zero warnings/errors.
9. Run all tests.
10. If Web publishing is affected, run a Release publish.
11. Report exactly what changed, what was intentionally left unchanged, and the verification results.

## Core Principle

Prefer the simplest design that makes responsibilities, invariants, ownership, and dependencies obvious. Architect-quality code is not code with the most layers or files; it is code where the right rule lives in the right place, duplication is controlled, framework conventions are respected, and future changes remain predictable.
