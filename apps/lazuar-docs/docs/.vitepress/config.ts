import { defineConfig } from "vitepress";

/**
 * Lazuar Hub product & integrator guides.
 * Source: apps/lazuar-docs/docs — refine freely; later publish as public docs site.
 *
 * Diagram format (S20): ASCII-only for this wave; Mermaid plugin optional later.
 */

const sidebar = [
  {
    text: "Start",
    collapsed: false,
    items: [
      { text: "Introduction", link: "/" },
      { text: "Product lines", link: "/guide/product-lines" },
      { text: "Concepts", link: "/guide/concepts" },
    ],
  },
  {
    text: "Integrations",
    collapsed: false,
    items: [
      { text: "Overview", link: "/integrations/" },
      { text: "Hosted Commerce checkout", link: "/integrations/hosted-checkout" },
      { text: "Payment flow", link: "/integrations/payment-flow" },
      { text: "Payments cashier (M2M)", link: "/integrations/payments-cashier" },
      { text: "Provision a workspace", link: "/integrations/provision" },
      { text: "Create a checkout", link: "/integrations/create-checkout" },
      { text: "Webhooks", link: "/integrations/webhooks" },
      { text: "API keys & scopes", link: "/integrations/api-keys" },
      { text: "Environments & public URLs", link: "/integrations/environments" },
      { text: "Aura as a reference client", link: "/integrations/aura-reference" },
      { text: "Run the sample app", link: "/integrations/run-sample-app" },
      { text: "Second-app checklist", link: "/integrations/second-app-checklist" },
    ],
  },
  {
    text: "Reference",
    collapsed: false,
    items: [
      { text: "Error codes", link: "/reference/error-codes" },
      { text: "Event catalog", link: "/reference/events" },
      { text: "OpenAPI & Scalar", link: "/reference/openapi" },
      { text: "Glossary", link: "/reference/glossary" },
      { text: "How to maintain", link: "/guide/how-to-maintain" },
    ],
  },
];

export default defineConfig({
  title: "Lazuar Hub Docs",
  description: "Integrator and product guides for Lazuar Hub",
  lang: "en-US",
  cleanUrls: true,
  // base: "/docs/", // enable when serving under a subpath

  head: [
    ["link", { rel: "icon", type: "image/svg+xml", href: "/favicon.svg?v=1" }],
    ["meta", { name: "theme-color", content: "#0f172a" }],
  ],

  themeConfig: {
    logo: "/favicon.svg",
    siteTitle: "Lazuar Hub",
    nav: [
      { text: "Guide", link: "/" },
      { text: "Payment flow", link: "/integrations/payment-flow" },
      { text: "Payments", link: "/integrations/payments-cashier" },
      { text: "Sample", link: "/integrations/run-sample-app" },
      { text: "Webhooks", link: "/integrations/webhooks" },
      {
        text: "API",
        items: [
          { text: "OpenAPI overview", link: "/reference/openapi" },
          {
            text: "Developers (Scalar)",
            link: "http://localhost:3000",
          },
        ],
      },
    ],
    sidebar,
    socialLinks: [],
    search: { provider: "local" },
    outline: { level: [2, 3] },
    footer: {
      message: "v1 guides — Payments cashier + hosted Commerce checkout. OpenAPI is reference, not onboarding.",
      copyright: "Lazuar",
    },
    editLink: false,
    lastUpdated: true,
  },

  markdown: {
    lineNumbers: false,
  },
});
