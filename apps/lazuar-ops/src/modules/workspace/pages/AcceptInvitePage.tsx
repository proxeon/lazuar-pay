import { useEffect, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { Loader2 } from "lucide-react";
import { client, type EntitlementDto } from "../../../lib/api-client";

type View =
  | { kind: "loading" }
  | { kind: "missing" }
  | { kind: "success" }
  | { kind: "error"; message: string; wrongEmail: boolean };

type AcceptOutcome =
  | { kind: "unauth" }
  | { kind: "success"; workspaceId: string | null }
  | { kind: "error"; message: string; wrongEmail: boolean };

const acceptByToken = new Map<string, Promise<AcceptOutcome>>();

function loginReturnUrl(token: string) {
  return `/login?returnUrl=${encodeURIComponent(`/accept-invite?token=${token}`)}`;
}

async function acceptInvite(token: string): Promise<AcceptOutcome> {
  const existing = acceptByToken.get(token);
  if (existing) return existing;

  const attempt = (async (): Promise<AcceptOutcome> => {
    const { data: me, error: meError, response: meResponse } = await client.GET("/one/auth/me");
    if (meResponse.status === 401 || meError || !me) return { kind: "unauth" };

    const previous = await client.GET("/one/me/entitlements");
    const previousIds = new Set((previous.data ?? []).map((e) => e.workspace_id));

    const { error, response } = await client.POST("/one/workspaces/invites/accept", {
      body: { token },
    });

    if (response.status === 401) return { kind: "unauth" };

    if (response.status >= 500) {
      acceptByToken.delete(token);
      return {
        kind: "error",
        message: "Something went wrong accepting this invite. Try again.",
        wrongEmail: false,
      };
    }

    if (error) {
      acceptByToken.delete(token);
      const detail = error.detail || "Unable to accept this invitation.";
      return {
        kind: "error",
        message: detail,
        wrongEmail: /different email/i.test(detail),
      };
    }

    const next = await client.GET("/one/me/entitlements");
    const entitlements = (next.data ?? []) as EntitlementDto[];
    const joined = entitlements.find((e) => !previousIds.has(e.workspace_id));
    const workspaceId = joined?.workspace_id ?? (entitlements.length === 1 ? entitlements[0].workspace_id : null);
    return { kind: "success", workspaceId };
  })();

  acceptByToken.set(token, attempt);
  return attempt;
}

export default function AcceptInvitePage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const token = (searchParams.get("token") ?? "").trim();
  const [view, setView] = useState<View>(token ? { kind: "loading" } : { kind: "missing" });

  useEffect(() => {
    if (!token) return;

    let cancelled = false;
    let redirectTimer: number | undefined;

    void acceptInvite(token)
      .then((outcome) => {
        if (outcome.kind === "unauth") {
          acceptByToken.delete(token);
          if (!cancelled) navigate(loginReturnUrl(token));
          return;
        }

        if (outcome.kind === "error") {
          if (!cancelled) setView(outcome);
          return;
        }

        if (outcome.workspaceId) {
          localStorage.setItem("ops_active_workspace_id", outcome.workspaceId);
        }
        if (cancelled) return;
        setView({ kind: "success" });
        redirectTimer = window.setTimeout(() => {
          navigate("/commerce/dashboard", { replace: true });
        }, 800);
      })
      .catch(() => {
        acceptByToken.delete(token);
        if (!cancelled) {
          setView({
            kind: "error",
            message: "Unable to accept this invitation. Try again.",
            wrongEmail: false,
          });
        }
      });

    return () => {
      cancelled = true;
      if (redirectTimer !== undefined) window.clearTimeout(redirectTimer);
    };
  }, [navigate, token]);

  const handleSignOut = async () => {
    acceptByToken.delete(token);
    await client.POST("/one/auth/logout");
    localStorage.removeItem("ops_active_workspace_id");
    navigate(loginReturnUrl(token));
  };

  return (
    <div className="flex h-screen w-full items-center justify-center bg-[#f5f5f5] font-sans">
      <div className="w-full max-w-[380px] mx-4">
        <div className="bg-white border border-[#e5e5e5] p-8">
          {view.kind === "loading" && (
            <div className="flex flex-col items-center text-center gap-4">
              <Loader2 size={18} className="animate-spin text-[#71717a]" />
              <p className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">
                Accepting invitation…
              </p>
              <p className="text-[13px] text-[#71717a]">Sign in with the invited email.</p>
            </div>
          )}

          {view.kind === "missing" && (
            <div className="text-center space-y-3">
              <h1 className="text-xl font-semibold tracking-tight text-[#09090b]">Invalid invite</h1>
              <p className="text-[13px] text-[#71717a]">This invite link is missing a token.</p>
              <Link to="/login" className="inline-block mt-4 text-[12px] font-semibold text-[#09090b] hover:underline">
                Sign in
              </Link>
            </div>
          )}

          {view.kind === "success" && (
            <div className="text-center space-y-3">
              <h1 className="text-xl font-semibold tracking-tight text-[#09090b]">You're in</h1>
              <p className="text-[13px] text-[#71717a]">You've joined the workspace.</p>
            </div>
          )}

          {view.kind === "error" && (
            <div className="text-center space-y-4">
              <div className="p-4 bg-rose-50 border border-rose-200 text-left">
                <p className="text-[10px] font-bold tracking-wide uppercase text-rose-600">{view.message}</p>
              </div>
              {view.wrongEmail && (
                <>
                  <p className="text-[13px] text-[#71717a]">Sign out and use the invited address.</p>
                  <button
                    type="button"
                    onClick={() => void handleSignOut()}
                    className="w-full h-11 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] transition-colors"
                  >
                    Sign out
                  </button>
                </>
              )}
              {!view.wrongEmail && (
                <Link
                  to="/login"
                  onClick={() => acceptByToken.delete(token)}
                  className="inline-block text-[12px] font-semibold text-[#09090b] hover:underline"
                >
                  Sign in
                </Link>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
