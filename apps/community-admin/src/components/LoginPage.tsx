import { useState } from "react";
import { client } from "../lib/api-client";

interface LoginPageProps {
  onLogin: (token: string, user: { email: string; name?: string; role: string }) => void;
}

export default function LoginPage({ onLogin }: LoginPageProps) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [isLoading, setIsLoading] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    setIsLoading(true);

    try {
      const { data, error: apiError } = await client.POST("/platform/auth/login", {
        body: { email, password }
      });

      if (apiError) {
        throw new Error(apiError.detail || "Invalid credentials. Please try again.");
      }

      if (!data?.token) {
        throw new Error("Login failed. No token received.");
      }

      const user = data.user || { email, role: "SUPER_ADMIN" };
      onLogin(data.token, {
        email: user.email || email,
        name: user.name,
        role: user.role,
      });
    } catch (err: any) {
      setError(err.message || "Invalid credentials. Please try again.");
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <div className="flex h-screen w-full items-center justify-center bg-zinc-50 dark:bg-black font-sans">
      <div className="w-full max-w-[380px] mx-4">
        <div className="bg-card border border-border/60 rounded-none p-8 shadow-[8px_8px_0px_0px_rgba(0,0,0,0.05)] dark:shadow-none">
          <div className="text-center mb-8">
            <h1 className="text-xl font-bold tracking-tight text-foreground uppercase">Community Admin</h1>
            <p className="text-[11px] font-bold uppercase tracking-[0.2em] text-muted-foreground mt-2">Sign in to manage your MRR engine.</p>
          </div>

          {error && (
            <div className="mb-6 p-4 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-900 rounded-none">
              <p className="text-xs font-bold tracking-wide uppercase text-red-600 dark:text-red-400">{error}</p>
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-5">
            <div className="space-y-2">
              <label htmlFor="email" className="text-[11px] font-bold uppercase tracking-widest text-foreground">Email</label>
              <input
                id="email"
                type="email"
                required
                value={email}
                onChange={e => setEmail(e.target.value)}
                className="flex h-11 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                placeholder="admin@lazuar.com"
                autoComplete="email"
              />
            </div>

            <div className="space-y-2">
              <label htmlFor="password" className="text-[11px] font-bold uppercase tracking-widest text-foreground">Password</label>
              <input
                id="password"
                type="password"
                required
                value={password}
                onChange={e => setPassword(e.target.value)}
                className="flex h-11 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                placeholder="••••••••"
                autoComplete="current-password"
              />
            </div>

            <div className="pt-2">
              <button
                type="submit"
                disabled={isLoading}
                className="w-full h-12 bg-foreground text-background text-sm font-bold uppercase tracking-wide rounded-none hover:bg-foreground/90 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
              >
                {isLoading ? "Signing in..." : "Sign In"}
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  );
}
