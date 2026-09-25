# InvoiceFlow Engineering Guide

- Treat the approved Figma file as the visual source of truth. Translate it into semantic Razor and responsive CSS; do not copy generated React, Tailwind, absolute-layout output, device chrome, mock identity data, or temporary Figma asset URLs.
- Preserve the established Domain, Application, and Infrastructure architecture unless a concrete implementation issue requires a change. Domain entities must remain exactly six public properties, and calculated UI values must not be persisted on them.
- Razor components may use `InvoiceService` only. They must not access repositories, `DbContext`, Entity Framework, SQLite, or provider-specific exceptions.
- Organize Web code feature-first. Prefer focused components, but avoid wrappers and abstractions that do not earn their complexity.
- Keep page and component styling in CSS isolation. Keep global CSS limited to tokens, reset/base styles, shared primitives, validation, focus, and framework UI.
- Use semantic, accessible Razor. Keyboard operation, visible focus, meaningful labels and live regions, responsive reflow, adequate touch targets, and correct dialog focus behavior are required.
- Separate desktop and mobile presentation markup only when their semantics genuinely differ. Both presentations must share the same data, state, filtering, sorting, service calls, and handlers.
- Keep state local unless a narrowly scoped one-shot notification needs to cross navigation. Do not add Fluxor, Redux, MediatR, a UI framework, or another third-party state framework without approval.
- Store required SVG assets locally and use `currentColor` where appropriate. Self-host Geist only from its official distributable source and retain its license.
- When editor work begins, use Web-specific mutable editor models. Invoice numbers remain user-editable; do not invent automatic numbering. Zero-line-item invoices are valid.
- Never expose raw persistence exceptions in the UI. Translate only known failures at the approved architectural boundary and show user-safe messages.
- Do not modify completed architecture or add speculative modules, folders, routes, or features without a concrete reason.
