
# ADR 013: Frontend Module Implementation (ops-page)

**Status:** Active  
**Context:** As the Lazuar platform grows, we will introduce new business verticals (e.g., `Funnel`, `Vault`, `CRM`) to the `ops-page` Super App. To prevent the React codebase from becoming a tangled monolithic "Big Ball of Mud," we enforce strict domain-driven folder structures and routing boundaries.

This document outlines the standard operating procedure for adding a completely new module to the `ops-page` frontend.

---

## 1. Directory Structure Rule

Do not put feature-specific components in the root `src/components/` folder. The root components folder is strictly for global layout shells (e.g., `Sidebar.tsx`, `LoginPage.tsx`) and the AI Chat hibernation code.

When introducing a new module (e.g., `funnel`), you must create a dedicated directory under `src/modules/`:

```text
apps/ops-page/src/modules/funnel/
├── components/          # Reusable UI pieces specific ONLY to Funnel
├── pages/               # The actual React route components
│   ├── DashboardPage.tsx
│   ├── FunnelsPage.tsx
│   └── SettingsPage.tsx
```

## 2. Shared Core Utilities

Before building module-specific pages, always leverage the shared UI primitives located in `src/modules/core/`.

*   **`PageLayout.tsx`:** Every single page in your new module MUST be wrapped in this component. It guarantees a consistent sticky header, standardized `h1` typography, and built-in breadcrumb navigation across the entire Super App.

```tsx
import PageLayout from "../../core/components/PageLayout";

export default function FunnelsPage() {
  return (
    <PageLayout 
      title="Sales Funnels" 
      description="Manage your landing pages and conversion flows."
      breadcrumbs={[{ label: "Funnel", href: "/funnel/dashboard" }, { label: "Pages" }]}
      actionButton={<button>Create Funnel</button>}
    >
      {/* Page Content Here */}
    </PageLayout>
  );
}
```

## 3. Route Registration (`App.tsx`)

Every module must have its own isolated URL namespace. Do not use generic, top-level routes (e.g., avoid `/dashboard` or `/settings`).

1.  Open `apps/ops-page/src/App.tsx`.
2.  Import your new pages at the top.
3.  Add the nested routes inside the `<OpsLayout />` wrapper. The path must be prefixed with the module name.

```tsx
{/* Funnel Module Routes */}
<Route path="/funnel/dashboard" element={<FunnelDashboardPage />} />
<Route path="/funnel/pages" element={<FunnelsPage />} />
<Route path="/funnel/settings" element={<FunnelSettingsPage />} />
```

## 4. Sidebar Registration (`Sidebar.tsx`)

Once the routes are created, you must expose the module in the global sidebar navigation using the `ModuleNav` component.

1.  Open `apps/ops-page/src/components/Sidebar.tsx`.
2.  Locate the `<nav className="space-y-0.5">` section.
3.  Add a new `<ModuleNav />` block.
4.  Ensure the `basePath` matches the namespace you registered in `App.tsx`. This is critical: the sidebar relies on `basePath` to know when to auto-expand the accordion if the user is deep-linked into the module.

```tsx
<ModuleNav 
  title="Funnel" 
  basePath="/funnel" 
  icon={Filter} // Import an appropriate Lucide React icon
  links={[
    { label: "Dashboard", href: "/funnel/dashboard" },
    { label: "Landing Pages", href: "/funnel/pages" },
    { label: "Settings", href: "/funnel/settings" }
  ]} 
/>
```

## 5. Module Settings & Configuration

If your module requires configuration (e.g., attaching a custom domain to a funnel, or linking an email provider), do not clutter the sidebar with a dozen links.

Instead, create a single `SettingsLayout.tsx` specific to your module (e.g., `src/modules/funnel/pages/SettingsLayout.tsx`).

This layout should act as a secondary wrapper (placed *inside* the `PageLayout`) that renders a horizontal or vertical tab menu. This keeps the primary sidebar clean while allowing complex, multi-page settings inside the module.

## 6. API Client Integration

Always use the shared `openapi-fetch` client (`src/lib/api-client.ts`) for data fetching.

*   Do not write manual `fetch()` calls.
*   Rely on `@tanstack/react-query` for caching, loading states, and optimistic UI updates.
*   Ensure that any `useMutation` that performs a write action includes a `.catch()` block that extracts the `error.detail` from the standard backend `ProblemDetails` response and displays it via a `sonner` Toast notification.
