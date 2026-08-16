import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import Link from "next/link";
import { getCheckoutLocale } from "../modules/checkout/i18n/getCheckoutLocale";
import { t } from "../modules/checkout/i18n/translate";
import "./globals.css";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export async function generateMetadata(): Promise<Metadata> {
  const locale = await getCheckoutLocale();
  return {
    title: t(locale, "meta.title"),
    description: t(locale, "meta.description"),
    icons: {
      icon: [{ url: "/favicon.ico", sizes: "any" }, { url: "/favicon.svg", type: "image/svg+xml" }],
      apple: [{ url: "/apple-touch-icon.png", sizes: "180x180" }],
    },
  };
}

export default async function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  const locale = await getCheckoutLocale();
  const year = new Date().getFullYear();

  return (
    <html lang={locale} className={`${geistSans.variable} ${geistMono.variable}`} suppressHydrationWarning>
      <body className="min-h-screen flex flex-col antialiased bg-zinc-50 dark:bg-black text-foreground">
        
        <div className="flex-1 flex flex-col w-full">
          {children}
        </div>

        <footer className="w-full py-6 border-t border-border/60 bg-card mt-auto shrink-0">
          <div className="max-w-5xl mx-auto px-4 flex flex-col sm:flex-row items-center justify-between gap-4 text-xs font-medium text-muted-foreground">
            <p>{t(locale, "footer.copyright", { year })}</p>
            <div className="flex items-center gap-4 sm:gap-6">
              <Link href="/legal/terms" className="hover:text-foreground transition-colors">
                {t(locale, "footer.terms")}
              </Link>
              <Link href="/legal/privacy" className="hover:text-foreground transition-colors">
                {t(locale, "footer.privacy")}
              </Link>
              <Link href="/legal/refund" className="hover:text-foreground transition-colors">
                {t(locale, "footer.refund")}
              </Link>
            </div>
          </div>
        </footer>

      </body>
    </html>
  );
}
