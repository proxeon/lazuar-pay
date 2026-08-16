export type PublicWorkspaceBranding = {
  name: string;
  slug: string;
  logo_url?: string | null;
  primary_color?: string | null;
};

const API = process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080/api/v1";

export async function fetchWorkspaceBranding(tenantSlug: string): Promise<PublicWorkspaceBranding | null> {
  try {
    const res = await fetch(`${API}/public/one/${encodeURIComponent(tenantSlug)}/branding`, {
      next: { revalidate: 60 },
    });
    if (!res.ok) return null;
    return (await res.json()) as PublicWorkspaceBranding;
  } catch {
    return null;
  }
}
