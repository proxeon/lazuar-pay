import { Routes, Route, Navigate } from "react-router-dom";
import IdentityHubLayout from "./components/IdentityHubLayout";
import HubPage from "./pages/HubPage";
import ProfilePage from "./pages/ProfilePage";
import SecurityPage from "./pages/SecurityPage";
import AcceptInvitePage from "./pages/AcceptInvitePage";
// Assuming LoginPage and RegisterPage exist based on earlier auth flows
import Login from "./pages/Login"; 
import Register from "./pages/Register";

export default function App() {
  return (
    <Routes>
      {/* Public Auth Routes */}
      <Route path="/login" element={<Login />} />
      <Route path="/register" element={<Register />} />
      <Route path="/accept-invite" element={<AcceptInvitePage />} />

      {/* Authenticated Identity Hub Routes */}
      <Route element={<IdentityHubLayout />}>
        <Route path="/" element={<Navigate to="/hub" replace />} />
        <Route path="/hub" element={<HubPage />} />
        <Route path="/settings/profile" element={<ProfilePage />} />
        <Route path="/settings/security" element={<SecurityPage />} />
      </Route>

      <Route path="*" element={<Navigate to="/login" replace />} />
    </Routes>
  );
}
