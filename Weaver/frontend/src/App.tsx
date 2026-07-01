import { NavLink, Route, Routes } from "react-router-dom";
import Dashboard from "./pages/Dashboard";
import ScrapingProjectsList from "./pages/ScrapingProjectsList";
import ScrapingProjectEditor from "./pages/ScrapingProjectEditor";
import WorkflowsList from "./pages/WorkflowsList";
import WorkflowEditor from "./pages/WorkflowEditor";
import RateLimitPolicies from "./pages/RateLimitPolicies";

export default function App() {
  return (
    <>
      <aside className="sidebar">
        <h1>
          <span>Weaver</span>
        </h1>
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
        </nav>
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
        </Routes>
      </main>
    </>
  );
}
