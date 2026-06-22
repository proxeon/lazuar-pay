import { Routes, Route, Navigate } from "react-router-dom";
import LaunchpadPage from "./pages/LaunchpadPage";
import LoginPage from "./pages/LoginPage";
import RegisterPage from "./pages/RegisterPage";
import ProfilePage from "./pages/ProfilePage";
import AcceptInvitePage from "./pages/AcceptInvitePage";
import ForgotPasswordPage from "./pages/ForgotPasswordPage";
import ResetPasswordPage from "./pages/ResetPasswordPage";
import VerifyEmailPage from "./pages/VerifyEmailPage";

export default function App() {
  return (
    <Routes>
      {/* Central Switchboard Route */}
      <Route path="/launchpad" element={<LaunchpadPage />} />

      {/* Global Identity Management & Security */}
      <Route path="/profile" element={<ProfilePage />} />

      {/* Authentication & Account Handshake Flows */}
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      
      {/* Password Recovery & Verification Pipelines */}
      <Route path="/forgot-password" element={<ForgotPasswordPage />} />
      <Route path="/reset-password" element={<ResetPasswordPage />} />
      <Route path="/verify-email" element={<VerifyEmailPage />} />

      {/* Workspace Invitation Acceptance */}
      <Route path="/accept-invite" element={<AcceptInvitePage />} />

      {/* Fallback routing */}
      <Route path="*" element={<Navigate to="/launchpad" replace />} />
    </Routes>
  );
}
