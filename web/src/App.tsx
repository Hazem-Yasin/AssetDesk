import { BrowserRouter, Link, NavLink, Navigate, Outlet, Route, Routes, useLocation } from 'react-router-dom';
import { AuthProvider, useAuth } from './auth';
import AssetDetailPage from './pages/AssetDetailPage';
import AssetFormPage from './pages/AssetFormPage';
import AssetListPage from './pages/AssetListPage';
import DashboardPage from './pages/DashboardPage';
import LoginPage from './pages/LoginPage';
import MyAssetsPage from './pages/MyAssetsPage';

/** Redirects anonymous users to /login (remembering where they were headed). */
export function RequireAuth({ adminOnly = false }: { adminOnly?: boolean }) {
  const { user, isAdmin } = useAuth();
  const location = useLocation();
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  if (adminOnly && !isAdmin) return <Navigate to="/" replace />;
  return <Outlet />;
}

function Layout() {
  const { user, logout } = useAuth();
  return (
    <>
      <header className="topbar">
        <Link to="/" className="brand">AssetDesk</Link>
        <nav>
          <NavLink to="/" end>Dashboard</NavLink>
          <NavLink to="/assets">Assets</NavLink>
          <NavLink to="/mine">My assets</NavLink>
        </nav>
        <span className="who">{user?.displayName} ({user?.role})</span>
        <button onClick={logout}>Sign out</button>
      </header>
      <main className="content"><Outlet /></main>
    </>
  );
}

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<RequireAuth />}>
        <Route element={<Layout />}>
          <Route index element={<DashboardPage />} />
          <Route path="assets" element={<AssetListPage />} />
          <Route path="assets/:id" element={<AssetDetailPage />} />
          <Route path="mine" element={<MyAssetsPage />} />
          <Route element={<RequireAuth adminOnly />}>
            <Route path="assets/new" element={<AssetFormPage />} />
            <Route path="assets/:id/edit" element={<AssetFormPage />} />
          </Route>
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <AppRoutes />
      </AuthProvider>
    </BrowserRouter>
  );
}
