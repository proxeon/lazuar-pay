import { Navigate, Route, Routes } from 'react-router-dom'
import { RequireAuth } from './auth/RequireAuth'
import { OrgLayout } from './layout/OrgLayout'
import { CallbackPage } from './pages/CallbackPage'
import { CreateWorkspacePage } from './pages/CreateWorkspacePage'
import { HomePage } from './pages/HomePage'
import { LoginPage } from './pages/LoginPage'
import { CheckoutsPage } from './pages/org/CheckoutsPage'
import { OrgCreateWorkspacePage } from './pages/org/CreateWorkspacePage'
import { GatewayPage } from './pages/org/GatewayPage'
import { OverviewPage } from './pages/org/OverviewPage'
import { PaymentsPage } from './pages/org/PaymentsPage'
import { ReceiptsPage } from './pages/org/ReceiptsPage'

export default function App() {
  return (
    <Routes>
      <Route path="/callback" element={<CallbackPage />} />
      <Route path="/login" element={<LoginPage />} />
      <Route
        path="/"
        element={
          <RequireAuth>
            <HomePage />
          </RequireAuth>
        }
      />
      <Route
        path="/workspaces/new"
        element={
          <RequireAuth>
            <CreateWorkspacePage />
          </RequireAuth>
        }
      />
      <Route
        path="/o/:orgId"
        element={
          <RequireAuth>
            <OrgLayout />
          </RequireAuth>
        }
      >
        <Route index element={<Navigate to="overview" replace />} />
        <Route path="overview" element={<OverviewPage />} />
        <Route path="new" element={<OrgCreateWorkspacePage />} />
        <Route path="gateway" element={<GatewayPage />} />
        <Route path="checkouts" element={<CheckoutsPage />} />
        <Route path="payments" element={<PaymentsPage />} />
        <Route path="receipts" element={<ReceiptsPage />} />
      </Route>
    </Routes>
  )
}
