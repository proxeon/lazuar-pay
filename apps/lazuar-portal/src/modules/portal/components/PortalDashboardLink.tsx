"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { ReactNode } from "react";

export function PortalDashboardLink({
  tenantSlug,
  children,
  className,
}: {
  tenantSlug: string;
  children: ReactNode;
  className?: string;
}) {
  const params = useSearchParams();
  const token = params.get("token");
  const href = token
    ? `/${tenantSlug}/portal?token=${encodeURIComponent(token)}`
    : `/${tenantSlug}/portal`;
  return (
    <Link href={href} className={className}>
      {children}
    </Link>
  );
}
