import Link from "next/link";
import { CheckCircle2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { getPlanBySlug } from "@/lib/api";
import { notFound } from "next/navigation";

export default async function SuccessPage({ params }: { params: Promise<{ slug: string }> }) {
  const resolvedParams = await params;
  const pkg = await getPlanBySlug(resolvedParams.slug);

  if (!pkg) {
    notFound();
  }

  return (
    <div className="min-h-screen flex flex-col bg-zinc-50 dark:bg-black items-center justify-center p-4">
      <div className="bg-card border border-border/60 shadow-sm p-8 sm:p-12 rounded-none max-w-md w-full text-center">
        <div className="flex items-center justify-center w-16 h-16 bg-emerald-50 dark:bg-emerald-950/30 rounded-full mx-auto mb-6">
          <CheckCircle2 className="h-8 w-8 text-emerald-600 dark:text-emerald-500" />
        </div>
        <h1 className="text-2xl font-semibold text-foreground mb-3">Payment Successful!</h1>
        <p className="text-sm text-muted-foreground mb-8 leading-relaxed">
          You are now subscribed to <strong>{pkg.name}</strong>. Please check your email and WhatsApp for your private community links and instructions.
        </p>
        <Link href={`/${pkg.slug}`} className="block w-full">
          <Button className="w-full h-12 text-sm font-bold tracking-wide uppercase bg-foreground text-background hover:bg-foreground/90 rounded-none">
            Return to Program
          </Button>
        </Link>
      </div>
    </div>
  );
}
