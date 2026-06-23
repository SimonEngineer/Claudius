import { useEffect, useState } from "react"
import { Link, useNavigate } from "react-router-dom"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardFooter, CardHeader, CardTitle, CardDescription } from "@/components/ui/card"
import { deleteProject, listProjects } from "@/api/projectsApi"
import type { ProjectSummary } from "@/types"

export function ProjectListPage() {
  const [projects, setProjects] = useState<ProjectSummary[]>([])
  const [error, setError] = useState<string | null>(null)
  const navigate = useNavigate()

  function refresh() {
    listProjects()
      .then(setProjects)
      .catch((err) => setError(err instanceof Error ? err.message : "Failed to load projects"))
  }

  useEffect(refresh, [])

  async function handleDelete(id: number) {
    if (!confirm("Delete this project?")) return
    await deleteProject(id)
    refresh()
  }

  return (
    <div className="min-h-svh bg-background p-6">
      <div className="flex items-center justify-between mb-4">
        <h1 className="text-xl font-semibold">Projects</h1>
        <Button onClick={() => navigate("/projects/new")}>New project</Button>
      </div>

      {error && <p className="text-sm text-destructive mb-4">{error}</p>}

      {projects.length === 0 && !error && (
        <p className="text-sm text-muted-foreground">No saved projects yet.</p>
      )}

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        {projects.map((project) => (
          <Card key={project.id}>
            <CardHeader>
              <CardTitle>{project.name}</CardTitle>
              <CardDescription>
                {project.shapeType} · {project.nameCount} name{project.nameCount === 1 ? "" : "s"}
              </CardDescription>
            </CardHeader>
            <CardContent>
              <p className="text-sm text-muted-foreground">
                Created {new Date(project.createdAt).toLocaleDateString()}
              </p>
            </CardContent>
            <CardFooter className="gap-2">
              <Button size="sm" asChild>
                <Link to={`/projects/${project.id}`}>Open</Link>
              </Button>
              <Button size="sm" variant="destructive" onClick={() => handleDelete(project.id)}>
                Delete
              </Button>
            </CardFooter>
          </Card>
        ))}
      </div>
    </div>
  )
}
