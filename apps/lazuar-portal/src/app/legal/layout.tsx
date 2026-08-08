"use client";

import { useRouter } from "next/navigation";
import { ArrowLeft } from "lucide-react";

export default function LegalLayout({ children }: { children: React.ReactNode }) {
  const router = useRouter();

  return (
    <div className="min-h-screen flex flex-col bg-zinc-50 dark:bg-black">
      <header className="sticky top-0 z-40 w-full bg-card border-b border-border/60 shadow-sm">
        <div className="max-w-3xl mx-auto px-4 h-14 flex items-center">
          <button 
            onClick={() => router.back()} 
            className="inline-flex items-center gap-2 -ml-2 px-2 py-1.5 text-muted-foreground hover:text-foreground transition-all group rounded-none focus:outline-none"
          >
            <ArrowLeft className="h-4 w-4 transition-transform group-hover:-translate-x-0.5" />
            <span className="text-sm font-medium">Back</span>
          </button>
        </div>
      </header>

      <main className="flex-1 w-full max-w-3xl mx-auto px-4 py-8 md:py-12">
        <div className="bg-card border border-border/60 shadow-sm p-6 sm:p-8 md:p-12 rounded-none">
          {children}
        </div>
      </main>
    </div>
  );
}
