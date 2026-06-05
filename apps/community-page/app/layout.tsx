import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import { Toaster } from "sonner";
import Link from "next/link";
import "./globals.css";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "Community Subscriptions | Lazuar",
  description: "Secure your monthly access.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html
      lang="en"
      className={`${geistSans.variable} ${geistMono.variable}`}
      suppressHydrationWarning
    >
      <body className="min-h-screen flex flex-col bg-zinc-50 dark:bg-black text-foreground selection:bg-foreground selection:text-background antialiased">

        <div className="flex flex-col flex-1 w-full">
          {children}
        </div>

        {/* Footer */}
        <footer className="w-full py-6 border-t border-border/60 bg-card">
          <div className="max-w-5xl mx-auto px-4 flex flex-col md:flex-row items-center justify-between gap-4 text-xs font-medium text-muted-foreground">
            <p>© {new Date().getFullYear()} Lazuar Education. All rights reserved.</p>
            <div className="flex items-center gap-4">
              <Link href="/legal/terms" className="hover:text-foreground transition-colors">
                Terms
              </Link>
              <Link href="/legal/privacy" className="hover:text-foreground transition-colors">
                Privacy
              </Link>
              <Link href="/legal/refund" className="hover:text-foreground transition-colors">
                Refund Policy
              </Link>
            </div>
          </div>
        </footer>

        <Toaster position="top-center" theme="system" />
      </body>
    </html>
  );
}
