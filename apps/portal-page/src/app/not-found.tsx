// apps/portal-page/src/app/not-found.tsx
import Link from "next/link";

export default function NotFound() {
  return (
    <div className="flex flex-col items-center justify-center min-h-screen bg-zinc-50 dark:bg-black font-sans p-4 text-center selection:bg-foreground selection:text-background antialiased">
      <div className="max-w-md w-full">
        <h1 className="text-6xl font-bold tracking-tighter text-foreground mb-2">404</h1>
        <h2 className="text-lg font-semibold tracking-tight text-foreground mb-4">Resource Not Found</h2>
        <p className="text-sm text-muted-foreground leading-relaxed mb-8">
          The checkout page or portal you are looking for does not exist, has been archived, or the link has expired.
        </p>
        <Link href="/">
          <button className="h-10 px-6 border border-border bg-background hover:bg-accent hover:text-accent-foreground text-xs font-bold uppercase tracking-widest transition-colors rounded-none">
            Return Home
          </button>
        </Link>
      </div>
    </div>
  );
}
