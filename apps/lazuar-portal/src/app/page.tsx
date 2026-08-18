// apps/lazuar-portal/src/app/page.tsx
export default function GlobalLandingPage() {
  return (
    <div className="flex flex-col items-center justify-center min-h-screen bg-zinc-50 dark:bg-black font-sans p-4 text-center selection:bg-foreground selection:text-background antialiased">
      <div className="max-w-md w-full bg-white dark:bg-zinc-950 border border-border p-8 md:p-12 shadow-sm rounded-none">
        <div className="mb-8">
          <svg className="h-8 w-8 mx-auto text-foreground mb-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
            <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
          </svg>
          <h1 className="text-xl font-semibold tracking-tight text-foreground">Lazuar Secure Portal</h1>
        </div>
        <p className="text-sm text-muted-foreground leading-relaxed">
          Please use the secure, personal magic links sent to your email to access your subscriptions and receipts.
        </p>
      </div>
    </div>
  );
}
