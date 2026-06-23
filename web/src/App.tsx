import { Link, Route, Routes } from "react-router-dom";

import { ApprovalInboxPage } from "@/pages/approval-inbox-page";
import { ProjectDetailPage } from "@/pages/project-detail-page";
import { ProjectListPage } from "@/pages/project-list-page";
import { TaskDetailPage } from "@/pages/task-detail-page";

function App() {
  return (
    <div className="flex min-h-screen flex-col">
      <header className="border-b">
        <div className="container flex h-14 items-center justify-between">
          <Link to="/" className="font-semibold">
            Claudius
          </Link>
          <nav className="flex gap-4 text-sm">
            <Link to="/" className="text-muted-foreground hover:text-foreground">
              Projects
            </Link>
            <Link to="/approvals" className="text-muted-foreground hover:text-foreground">
              Approvals
            </Link>
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
