import { BrowserRouter, Route, Routes } from "react-router-dom"
import { ProjectListPage } from "@/pages/ProjectListPage"
import { ProjectEditorPage } from "@/pages/ProjectEditorPage"
import { ExperimentalEditorPage } from "@/pages/ExperimentalEditorPage"

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<ProjectListPage />} />
        <Route path="/projects/new" element={<ProjectEditorPage />} />
        <Route path="/projects/:id" element={<ProjectEditorPage />} />
        <Route path="/experimental" element={<ExperimentalEditorPage />} />
      </Routes>
    </BrowserRouter>
  )
}

export default App
