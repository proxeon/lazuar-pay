namespace LazuarApi.Messaging;

// Intentionally thin (models-only, no routes).
// Messaging is dispatch/outbox ownership — not a public HTTP product surface.
// Templates, broadcasts, and customer-facing copy live under Communications.
// HTTP notify/logs (if any) are internal Minimal API and are not TypeSpec-documented.
// Keep this file so main.tsp can import the namespace without inventing phantom DTOs.
