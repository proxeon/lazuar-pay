import { useNavigate, useSearchParams } from "react-router-dom";
import { client, OPS_URL } from "../lib/api-client";
import { isValidReturnUrl } from "../lib/utils";

export function useAuth() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

  const handleSmartRouting = async () => {
    const returnUrl = searchParams.get("returnUrl");

    if (returnUrl && isValidReturnUrl(returnUrl)) {
      window.location.href = returnUrl;
      return;
    }

    try {
      const { data: entitlements, error } = await client.GET("/one/me/entitlements");

      if (!error && entitlements && entitlements.length > 0) {
        const isPowerUser = entitlements.some(
          (e) => e.role === "ADMIN" || e.role === "SUPER_ADMIN"
        );

        if (isPowerUser) {
          window.location.href = OPS_URL;
          return;
        }
      }
    } catch (err) {
      console.error("Failed to evaluate entitlements for routing", err);
    }

    navigate("/hub");
  };

  return { handleSmartRouting };
}
