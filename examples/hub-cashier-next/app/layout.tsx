import type { Metadata } from "next";
import type { ReactNode } from "react";
import "./globals.css";

export const metadata: Metadata = {
  title: "Hub Cashier Sample (Next.js)",
  description:
    "Teachable Next.js sample that uses Lazuar Hub as a multi-app payments cashier. Not production software.",
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en">
      <body>
        <main>
          <p className="badge">Sample · not production</p>
          {children}
        </main>
      </body>
    </html>
  );
}
