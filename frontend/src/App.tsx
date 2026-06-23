import { ModelViewer } from "@/components/ModelViewer/ModelViewer"

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5050"

function App() {
  return (
    <div className="min-h-svh bg-background p-6">
      <h1 className="text-xl font-semibold mb-4">Name Tags</h1>
      <div className="h-[480px] rounded-lg border overflow-hidden">
        <ModelViewer stlUrl={`${API_BASE}/api/debug/box.stl`} />
      </div>
    </div>
  )
}

export default App
