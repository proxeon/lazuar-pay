const SERVER_API_URL = process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080/api/v1";
const CLIENT_API_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080/api/v1";
const TENANT_SLUG = process.env.NEXT_PUBLIC_TENANT_SLUG || "lazuar-hq";

export interface CommunityPlan {
  id: string;
  slug: string;
  name: string;
  audience: string;
  short_description: string;
  long_description: string;
  price: number;
  interval: string;
  features: string[];
  methodology: string;
  faq: { id: string; question: string; answer: string }[];
  max_capacity: number | null;
  enrolled_count: number;
  spots_remaining: number | null;
  is_full: boolean;
}

export interface PortalData {
  customer: {
    name: string;
    email: string;
    phone: string;
  };
  subscriptions: Array<{
    id: string;
    status: string;
    current_period_end: string | null;
    next_billing_date: string | null;
    plan: {
      name: string;
      slug: string;
      price: number;
      interval: string;
    };
    payments: Array<{
      id: string;
      amount: number;
      currency: string;
      payment_method: string;
      status: string;
      created_at: string;
    }>;
  }>;
}

export async function getPlans(): Promise<CommunityPlan[]> {
  // GET /public/community/{tenantSlug}/plans (Route param standardized)
  const res = await fetch(`${SERVER_API_URL}/public/community/${TENANT_SLUG}/plans`, {
    cache: "no-store"
  });
  if (!res.ok) throw new Error("Failed to fetch plans");
  return res.json();
}

export async function getPlanBySlug(slug: string): Promise<CommunityPlan | null> {
  // GET /public/community/{tenantSlug}/plans/{slug} (Route param standardized)
  const res = await fetch(`${SERVER_API_URL}/public/community/${TENANT_SLUG}/plans/${slug}`, {
    cache: "no-store"
  });
  if (!res.ok) {
    if (res.status === 404) return null;
    throw new Error("Failed to fetch plan details");
  }
  return res.json();
}

export async function createCheckoutSession(data: { plan_slug: string; name: string; email: string; phone: string }): Promise<string> {
  // POST /public/community/checkout (Payload contract matched)
  const res = await fetch(`${CLIENT_API_URL}/public/community/checkout`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ 
      tenant_slug: TENANT_SLUG,
      plan_slug: data.plan_slug,
      name: data.name,
      email: data.email,
      phone: data.phone
    }),
  });

  const json = await res.json();
  if (!res.ok) throw new Error(json.error || "Checkout failed");
  return json.url;
}

// ========================================================
// PORTAL API
// ========================================================

export async function requestMagicLink(email: string): Promise<void> {
  // POST /public/community/{tenantSlug}/portal/magic-link (Route param standardized)
  const res = await fetch(`${CLIENT_API_URL}/public/community/${TENANT_SLUG}/portal/magic-link`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email }),
  });
  if (!res.ok) throw new Error("Failed to request magic link");
}

export async function getPortalData(token: string): Promise<PortalData> {
  // GET /public/community/{tenantSlug}/portal?token={token} (Route param standardized)
  const res = await fetch(`${CLIENT_API_URL}/public/community/${TENANT_SLUG}/portal?token=${encodeURIComponent(token)}`);
  if (!res.ok) throw new Error("Unauthorized or expired link");
  return res.json();
}

export async function updatePortalContact(token: string, data: { name: string; email: string; phone: string }): Promise<void> {
  // PUT /public/community/{tenantSlug}/portal/contact?token={token} (Route param standardized)
  const res = await fetch(`${CLIENT_API_URL}/public/community/${TENANT_SLUG}/portal/contact?token=${encodeURIComponent(token)}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(data),
  });
  if (!res.ok) throw new Error("Failed to update contact details");
}

export async function cancelPortalSubscription(token: string, subscriptionId: string): Promise<void> {
  // POST /public/community/{tenantSlug}/portal/cancel?token={token} (Route param standardized)
  const res = await fetch(`${CLIENT_API_URL}/public/community/${TENANT_SLUG}/portal/cancel?token=${encodeURIComponent(token)}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ subscription_id: subscriptionId })
  });
  if (!res.ok) throw new Error("Failed to cancel subscription");
}
