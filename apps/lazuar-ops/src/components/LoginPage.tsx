import { useState, useEffect } from "react";
import { Link, useLocation, useSearchParams } from "react-router-dom";
import { Loader2 } from "lucide-react";
import { client } from "../lib/api-client";
import { slugify, validateSlug } from "../lib/workspace-slug";

type AuthMode = "signin" | "signup";

const LEGAL_TERMS_HREF = "/portal/legal/terms";
const LEGAL_PRIVACY_HREF = "/portal/legal/privacy";

function isSafeReturnUrl(value: string): boolean {
  return value.startsWith("/") && !value.startsWith("//");
}

export default function LoginPage() {
  const [searchParams] = useSearchParams();
  const location = useLocation();
  const forcedSignup = location.pathname.endsWith("/signup") || searchParams.get("mode") === "signup";
  const [mode, setMode] = useState<AuthMode>(forcedSignup ? "signup" : "signin");
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState("");

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [workspaceName, setWorkspaceName] = useState("");
  const [tenantSlug, setTenantSlug] = useState("");
  /** When true, typing workspace name no longer overwrites a hand-edited slug */
  const [slugTouched, setSlugTouched] = useState(false);
  const [acceptedTerms, setAcceptedTerms] = useState(false);

  const rawReturnUrl = searchParams.get("returnUrl");
  const returnUrl = rawReturnUrl && isSafeReturnUrl(rawReturnUrl) ? rawReturnUrl : null;
  const signupHref = returnUrl ? `/signup?returnUrl=${encodeURIComponent(returnUrl)}` : "/signup";
  const loginHref = returnUrl ? `/login?returnUrl=${encodeURIComponent(returnUrl)}` : "/login";
  const inviteReturn = returnUrl?.startsWith("/accept-invite") ?? false;

  useEffect(() => {
    localStorage.removeItem("ops_active_workspace_id");
  }, []);

  useEffect(() => {
    setMode(forcedSignup ? "signup" : "signin");
  }, [forcedSignup]);

  const handleWorkspaceNameChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const val = e.target.value;
    setWorkspaceName(val);
    if (!slugTouched) {
      setTenantSlug(slugify(val));
    }
  };

  const handleSlugChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setSlugTouched(true);
    setTenantSlug(slugify(e.target.value));
  };

  const handleLoginSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError("");

    try {
      const { error: apiError } = await client.POST("/one/auth/login", {
        body: { email, password },
      });

      if (apiError) throw new Error(apiError.detail || "Invalid credentials.");

      window.location.href = returnUrl ?? "/commerce/dashboard";
    } catch (err: any) {
      setError(err.message || "Invalid credentials.");
    } finally {
      setIsLoading(false);
    }
  };

  const handleSignUpSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError("");

    if (password !== confirmPassword) {
      setError("Passwords do not match.");
      setIsLoading(false);
      return;
    }

    const slug = slugify(tenantSlug);
    setTenantSlug(slug);
    const slugError = validateSlug(slug);
    if (slugError) {
      setError(slugError);
      setIsLoading(false);
      return;
    }

    if (!workspaceName.trim()) {
      setError("Workspace name is required.");
      setIsLoading(false);
      return;
    }

    if (!acceptedTerms) {
      setError("You must accept the Terms of Service and Privacy Policy.");
      setIsLoading(false);
      return;
    }

    try {
      const { error: registerError } = await client.POST("/one/public/register", {
        body: {
          email,
          password,
          name: email.split("@")[0],
          workspace_name: workspaceName.trim(),
          tenant_slug: slug,
          accepted_terms: true,
        },
      });

      if (registerError) throw new Error(registerError.detail || "Registration failed.");

      window.location.href = returnUrl ?? "/commerce/dashboard";
    } catch (err: any) {
      setError(err.message || "Registration failed.");
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="flex h-screen w-full items-center justify-center bg-[#f5f5f5] font-sans">
      <div className="w-full max-w-[380px] mx-4 animate-in fade-in zoom-in-95 duration-300">
        <div className="bg-white border border-[#e5e5e5] p-8 rounded-none">
          {error && (
            <div className="mb-6 p-4 bg-rose-50 border border-rose-200">
              <p className="text-[10px] font-bold tracking-wide uppercase text-rose-600">{error}</p>
            </div>
          )}

          {mode === "signin" && (
            <div className="animate-in fade-in slide-in-from-left-4 duration-300">
              <div className="text-center mb-8">
                <h1 className="text-xl font-semibold tracking-tight text-[#09090b]">Sign in to Lazuar</h1>
                <p className="text-[13px] text-[#71717a] mt-1.5">
                  {inviteReturn ? "Sign in with the invited email." : "Welcome back to your ecosystem."}
                </p>
              </div>

              <form onSubmit={handleLoginSubmit} className="space-y-4">
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Email</label>
                  <input
                    type="email"
                    required
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    className="flex h-11 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]"
                    placeholder="name@example.com"
                  />
                </div>

                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Password</label>
                  <input
                    type="password"
                    required
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    className="flex h-11 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]"
                    placeholder="••••••••"
                  />
                </div>

                <button
                  type="submit"
                  disabled={isLoading}
                  className="w-full h-11 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest rounded-none flex items-center justify-center hover:bg-[#27272a] disabled:opacity-50 transition-colors mt-2"
                >
                  {isLoading ? <Loader2 size={16} className="animate-spin" /> : "Sign In"}
                </button>
              </form>

              <div className="mt-8 text-center space-y-2">
                <p className="text-[12px] text-[#71717a]">
                  <Link to="/forgot-password" className="text-[#09090b] font-semibold hover:underline">
                    Forgot password?
                  </Link>
                </p>
                <p className="text-[12px] text-[#71717a]">
                  Don't have an account?{" "}
                  <Link to={signupHref} className="text-[#09090b] font-semibold hover:underline">
                    Sign up
                  </Link>
                </p>
                <p className="text-[12px] text-[#71717a]">
                  <Link to="/pricing" className="text-[#09090b] font-semibold hover:underline">
                    See pricing
                  </Link>
                </p>
              </div>
            </div>
          )}

          {mode === "signup" && (
            <div className="animate-in fade-in slide-in-from-right-4 duration-300">
              <div className="text-center mb-8">
                <h1 className="text-xl font-semibold tracking-tight text-[#09090b]">Create Account</h1>
                <p className="text-[13px] text-[#71717a] mt-1.5">
                  {inviteReturn
                    ? "Sign in with the invited email."
                    : "Register a global identity and workspace."}
                </p>
              </div>

              <form onSubmit={handleSignUpSubmit} className="space-y-4">
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Workspace Name</label>
                  <input
                    type="text"
                    required
                    value={workspaceName}
                    onChange={handleWorkspaceNameChange}
                    className="flex h-11 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]"
                    placeholder="e.g. Acme Corp"
                  />
                </div>

                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Workspace URL slug</label>
                  <input
                    type="text"
                    required
                    minLength={3}
                    maxLength={63}
                    pattern="[a-z0-9]+(?:-[a-z0-9]+)*"
                    value={tenantSlug}
                    onChange={handleSlugChange}
                    className="flex h-11 w-full rounded-none border border-[#e5e5e5] bg-[#fafafa] px-3 py-1 font-mono text-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]"
                    placeholder="acme-corp"
                  />
                  <p className="text-[11px] text-[#a1a1aa]">
                    3–63 chars: a–z, 0–9, hyphens. Not reserved (login, admin, portal, …).
                  </p>
                </div>

                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Email Address</label>
                  <input
                    type="email"
                    required
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    className="flex h-11 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]"
                    placeholder="name@example.com"
                  />
                </div>

                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Password</label>
                  <input
                    type="password"
                    required
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    className="flex h-11 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]"
                    placeholder="••••••••"
                  />
                </div>

                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Confirm Password</label>
                  <input
                    type="password"
                    required
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                    className="flex h-11 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]"
                    placeholder="••••••••"
                  />
                </div>

                <label className="flex items-start gap-2.5 pt-1">
                  <input
                    type="checkbox"
                    required
                    checked={acceptedTerms}
                    onChange={(e) => setAcceptedTerms(e.target.checked)}
                    className="mt-0.5 h-4 w-4 rounded-none border-[#e5e5e5]"
                  />
                  <span className="text-[12px] text-[#71717a] leading-relaxed">
                    I agree to the{" "}
                    <a href={LEGAL_TERMS_HREF} target="_blank" rel="noreferrer" className="text-[#09090b] font-semibold hover:underline">
                      Terms of Service
                    </a>{" "}
                    and{" "}
                    <a href={LEGAL_PRIVACY_HREF} target="_blank" rel="noreferrer" className="text-[#09090b] font-semibold hover:underline">
                      Privacy Policy
                    </a>
                    . Platform use is covered by these pages until a merchant MSA exists.
                  </span>
                </label>

                <button
                  type="submit"
                  disabled={isLoading}
                  className="w-full h-11 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest rounded-none flex items-center justify-center hover:bg-[#27272a] disabled:opacity-50 transition-colors mt-2"
                >
                  {isLoading ? <Loader2 size={16} className="animate-spin" /> : "Create workspace"}
                </button>
              </form>

              <div className="mt-8 text-center space-y-2">
                <p className="text-[12px] text-[#71717a]">
                  Already have an account?{" "}
                  <Link to={loginHref} className="text-[#09090b] font-semibold hover:underline">
                    Sign in
                  </Link>
                </p>
                <p className="text-[12px] text-[#71717a]">
                  <Link to="/pricing" className="text-[#09090b] font-semibold hover:underline">
                    See pricing
                  </Link>
                </p>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
