import Link from "next/link";
import { ArrowRight, Users } from "lucide-react";
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { serverClient, TENANT_SLUG } from "@/lib/api-client";

export default async function CatalogPage() {
  const { data: plans, error } = await serverClient.GET("/public/community/{tenantSlug}/plans", {
    params: { path: { tenantSlug: TENANT_SLUG } },
    next: { revalidate: 60 } // Cache and revalidate every 60 seconds
  });

  if (error || !plans) {
    return <div className="p-8 text-center text-red-500">Failed to load community programs.</div>;
  }

  return (
    <main className="w-full max-w-5xl mx-auto px-4 py-12 md:py-20 flex-1">
      <div className="mb-12 text-center md:text-left">
        <h1 className="text-3xl md:text-4xl font-semibold tracking-tight text-foreground mb-4">
          Community Programs
        </h1>
        <p className="text-base text-muted-foreground max-w-2xl mx-auto md:mx-0 leading-relaxed">
          Select a monthly program below to view details, curriculum, and pricing. Pause or cancel your membership at any time.
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {plans.map((pkg) => (
          <Link
            href={pkg.is_full ? "#" : `/${pkg.slug}`}
            key={pkg.id}
            className={`block group ${pkg.is_full ? "pointer-events-none" : ""}`}
            aria-disabled={pkg.is_full}
          >
            <Card className={`flex flex-col h-full transition-all duration-200 bg-card border-border/60 rounded-none ${
              pkg.is_full ? "opacity-60 border-border/40" : "hover:border-foreground/40 hover:shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)] dark:hover:shadow-none"
            }`}>
              <CardHeader className="pb-5">
                <div className="mb-4 flex items-center justify-between">
                  <span className="inline-flex items-center border border-border/60 px-2 py-0.5 text-[10px] font-bold uppercase tracking-widest bg-secondary/30 text-muted-foreground rounded-none">
                    {pkg.audience}
                  </span>

                  {pkg.spots_remaining !== null && (
                    pkg.is_full ? (
                      <span className="inline-flex items-center gap-1 px-2 py-0.5 text-[10px] font-bold uppercase bg-red-50 text-red-600 rounded-none dark:bg-red-950 dark:text-red-400">
                        <Users className="h-3 w-3" /> Full
                      </span>
                    ) : (
                      <span className="inline-flex items-center gap-1 px-2 py-0.5 text-[10px] font-bold uppercase bg-amber-50 text-amber-700 rounded-none dark:bg-amber-950 dark:text-amber-400">
                        <Users className="h-3 w-3" /> {pkg.spots_remaining} left
                      </span>
                    )
                  )}
                </div>
                <CardTitle className="text-xl font-semibold leading-tight group-hover:text-foreground/80 transition-colors">
                  {pkg.name}
                </CardTitle>
              </CardHeader>
              <CardContent className="flex-1 pb-8">
                <p className="text-sm text-muted-foreground leading-relaxed">{pkg.short_description}</p>
              </CardContent>
              <CardFooter className="pt-0 mt-auto flex items-center justify-between border-t border-border/60 bg-zinc-50/50 dark:bg-zinc-900/20 pb-4 pt-4 rounded-none">
                {pkg.is_full ? (
                  <span className="text-sm font-semibold text-muted-foreground">Enrollment Closed</span>
                ) : (
                  <span className="text-sm font-semibold text-foreground tracking-wide">View Details</span>
                )}
                {!pkg.is_full && <ArrowRight className="h-4 w-4 text-muted-foreground group-hover:text-foreground transition-all group-hover:translate-x-1" />}
              </CardFooter>
            </Card>
          </Link>
        ))}
      </div>
    </main>
  );
}
