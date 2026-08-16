import { redirect } from "next/navigation";

export default async function AcceptInviteRedirectPage({
  searchParams,
}: {
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
  const params = await searchParams;
  const raw = params.token;
  const token = Array.isArray(raw) ? raw[0] : raw;
  const opsBase = (process.env.NEXT_PUBLIC_OPS_URL || "http://localhost:3003").replace(/\/$/, "");
  const dest =
    token && token.length > 0
      ? `${opsBase}/accept-invite?token=${encodeURIComponent(token)}`
      : `${opsBase}/accept-invite`;
  redirect(dest);
}
