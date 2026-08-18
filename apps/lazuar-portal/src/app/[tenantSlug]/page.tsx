import { redirect } from "next/navigation";

export default async function TenantIndexPage({
  params,
  searchParams,
}: {
  params: Promise<{ tenantSlug: string }>;
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
  const { tenantSlug } = await params;
  const query = await searchParams;
  const raw = query.token;
  const token = Array.isArray(raw) ? raw[0] : raw;
  const dest = token
    ? `/${tenantSlug}/portal?token=${encodeURIComponent(token)}`
    : `/${tenantSlug}/portal`;
  redirect(dest);
}
