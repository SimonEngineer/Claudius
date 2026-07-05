import { useEffect, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
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
import { DashboardApi, MetaApi } from "./api/endpoints";
import { onRunStatusChanged } from "./realtime/runStatusConnection";
import { applyTheme, getTheme, type Theme } from "./utils/theme";

declare const __APP_VERSION__: string | undefined;

function AppShell() {
  const { user, logout } = useAuth();
  const queryClient = useQueryClient();
  const [theme, setTheme] = useState<Theme>(getTheme());

  const stats = useQuery({ queryKey: ["dashboard-stats"], queryFn: DashboardApi.stats, refetchInterval: 30000 });
  const version = useQuery({ queryKey: ["api-version"], queryFn: MetaApi.version, staleTime: Infinity });

  useEffect(() => {
    return onRunStatusChanged(() => {
      queryClient.invalidateQueries({ queryKey: ["dashboard-stats"] });
    });
  }, [queryClient]);

  const running = (stats.data?.runningScrapes ?? 0) + (stats.data?.runningWorkflows ?? 0);

  const toggleTheme = () => {
    const next: Theme = theme === "dark" ? "light" : "dark";
    setTheme(next);
    applyTheme(next);
  };

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
            {running > 0 && (
              <span
                className="pill Running"
                style={{ marginLeft: 8, fontSize: 11 }}
                title={`${stats.data?.runningScrapes ?? 0} scrape(s), ${stats.data?.runningWorkflows ?? 0} workflow run(s) in flight`}
              >
                {running} running
              </span>
            )}
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
          <div className="navlink" onClick={toggleTheme} title="Toggle light/dark theme">
            {theme === "dark" ? "☾ Dark theme" : "☀ Light theme"}
          </div>
          <NavLink to="/account" className={({ isActive }) => `navlink${isActive ? " active" : ""}`}>
            <span style={{ overflow: "hidden", textOverflow: "ellipsis" }}>{user?.email}</span>
          </NavLink>
          <div className="navlink" onClick={logout}>
            Log out
          </div>
          <div className="muted" style={{ fontSize: 11, padding: "8px 10px 0" }}>
            api v{version.data?.version ?? "?"}
            {typeof __APP_VERSION__ === "string" ? ` · ui v${__APP_VERSION__}` : ""}
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
