import { useEffect, useState } from "react";
import { Link, Route, Routes } from "react-router-dom";

import { ApprovalInboxPage } from "@/pages/approval-inbox-page";
import { ProjectDetailPage } from "@/pages/project-detail-page";
import { ProjectListPage } from "@/pages/project-list-page";
import { TaskDetailPage } from "@/pages/task-detail-page";
import { api } from "@/lib/api";

function HealthIndicator() {
  const [healthy, setHealthy] = useState<boolean | null>(null);

  useEffect(() => {
    const check = () => api.getHealth().then(
      (h) => setHealthy(h.status === "healthy"),
      () => setHealthy(false),
    );
    check();
    const interval = setInterval(check, 30_000);
    return () => clearInterval(interval);
  }, []);

  return (
    <span className="flex items-center gap-2 text-xs text-muted-foreground">
      <span
        className={`h-2 w-2 rounded-full ${
          healthy === null ? "bg-muted-foreground" : healthy ? "bg-emerald-500" : "bg-destructive"
        }`}
      />
      {healthy === null ? "Checking..." : healthy ? "API healthy" : "API unreachable"}
    </span>
  );
}

function App() {
  return (
    <div className="flex min-h-screen flex-col">
      <header className="border-b">
        <div className="container flex h-14 items-center justify-between">
          <Link to="/" className="font-semibold">
            Claudius
          </Link>
          <nav className="flex items-center gap-4 text-sm">
            <Link to="/" className="text-muted-foreground hover:text-foreground">
              Projects
            </Link>
            <Link to="/approvals" className="text-muted-foreground hover:text-foreground">
              Approvals
            </Link>
            <HealthIndicator />
          </nav>
        </div>
      </header>
      <main className="container flex-1 px-0">
        <Routes>
          <Route path="/" element={<ProjectListPage />} />
          <Route path="/projects/:projectId" element={<ProjectDetailPage />} />
          <Route path="/tasks/:taskId" element={<TaskDetailPage />} />
          <Route path="/approvals" element={<ApprovalInboxPage />} />
        </Routes>
      </main>
    </div>
  );
}

export default App;
