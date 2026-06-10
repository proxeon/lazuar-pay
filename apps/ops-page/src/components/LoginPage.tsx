import { useEffect } from "react";

export default function LoginPage() {
  useEffect(() => {
    const returnUrl = encodeURIComponent(window.location.origin + "/chat");
    window.location.href = `http://localhost:3001/login?returnUrl=${returnUrl}`;
  }, []);

  return null;
}
