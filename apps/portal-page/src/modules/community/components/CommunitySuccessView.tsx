// apps/portal-page/src/modules/community/components/CommunitySuccessView.tsx
import Link from "next/link";
import { type CommunityPlanDto } from "../lib/api";

interface CommunitySuccessViewProps {
  tenantSlug: string;
  plan: CommunityPlanDto;
}

export function CommunitySuccessView({ tenantSlug, plan }: CommunitySuccessViewProps) {
  return (
    <div className="min-h-screen flex flex-col items-center justify-center p-4">
      <div className="bg-card border border-border/60 shadow-sm p-8 sm:p-12 rounded-none max-w-md w-full text-center">
        <div className="flex items-center justify-center w-16 h-16 bg-emerald-50 dark:bg-emerald-950/30 rounded-full mx-auto mb-6">
          <svg 
            className="h-8 w-8 text-emerald-600 dark:text-emerald-500" 
            fill="none" 
            viewBox="0 0 24 24" 
            stroke="currentColor" 
            strokeWidth={2}
          >
            <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
          </svg>
        </div>
        <h1 className="text-2xl font-semibold text-foreground mb-3">Payment Successful!</h1>
        <p className="text-sm text-muted-foreground mb-8 leading-relaxed">
          You are now subscribed to <strong className="text-foreground">{plan.name}</strong>. Please check your email and WhatsApp for your private community links and instructions.
        </p>
        <Link href={`/${tenantSlug}/community/portal`} className="block w-full">
          <button className="w-full h-12 text-sm font-bold tracking-wide uppercase bg-foreground text-background hover:bg-foreground/90 rounded-none transition-colors">
            Go to Member Portal
          </button>
        </Link>
      </div>
    </div>
  );
}
