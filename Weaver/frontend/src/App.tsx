import { Navigate, NavLink, Route, Routes } from "react-router-dom";
import Dashboard from "./pages/Dashboard";
import ScrapingProjectsList from "./pages/ScrapingProjectsList";
import ScrapingProjectEditor from "./pages/ScrapingProjectEditor";
import WorkflowsList from "./pages/WorkflowsList";
import WorkflowEditor from "./pages/WorkflowEditor";
import RateLimitPolicies from "./pages/RateLimitPolicies";
import Activity from "./pages/Activity";
import Account from "./pages/Account";
import Login from "./pages/Login";
import GlobalSearch from "./components/GlobalSearch";
import { useAuth } from "./auth/AuthContext";

function AppShell() {
  const { user, logout } = useAuth();

  return (
    <>
      <aside className="sidebar">
        <h1>
          <span>Weaver</span>
        </h1>
        <GlobalSearch />
        <nav>
          <NavLink to="/" end className={({ isActive }) => (isActive ? "active" : "")}>
            Dashboard
          </NavLink>
          <NavLink to="/scraping-projects" className={({ isActive }) => (isActive ? "active" : "")}>
            Scraping Projects
          </NavLink>
          <NavLink to="/workflows" className={({ isActive }) => (isActive ? "active" : "")}>
            Workflows
          </NavLink>
          <NavLink to="/rate-limits" className={({ isActive }) => (isActive ? "active" : "")}>
            Rate Limits
          </NavLink>
          <NavLink to="/activity" className={({ isActive }) => (isActive ? "active" : "")}>
            Activity
          </NavLink>
        </nav>
        <div style={{ marginTop: "auto", paddingTop: 16 }}>
          <NavLink to="/account" className={({ isActive }) => `navlink${isActive ? " active" : ""}`}>
            <span style={{ overflow: "hidden", textOverflow: "ellipsis" }}>{user?.email}</span>
          </NavLink>
          <div className="navlink" onClick={logout}>
            Log out
          </div>
        </div>
      </aside>
      <main className="main">
        <Routes>
          <Route path="/" element={<Dashboard />} />
          <Route path="/scraping-projects" element={<ScrapingProjectsList />} />
          <Route path="/scraping-projects/new" element={<ScrapingProjectEditor />} />
          <Route path="/scraping-projects/:id" element={<ScrapingProjectEditor />} />
          <Route path="/workflows" element={<WorkflowsList />} />
          <Route path="/workflows/new" element={<WorkflowEditor />} />
          <Route path="/workflows/:id" element={<WorkflowEditor />} />
          <Route path="/rate-limits" element={<RateLimitPolicies />} />
          <Route path="/activity" element={<Activity />} />
          <Route path="/account" element={<Account />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>
    </>
  );
}

export default function App() {
  const { user } = useAuth();

  return (
    <Routes>
      <Route path="/login" element={user ? <Navigate to="/" replace /> : <Login />} />
      <Route path="/*" element={user ? <AppShell /> : <Navigate to="/login" replace />} />
    </Routes>
  );
}
