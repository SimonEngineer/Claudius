import { useState } from "react"
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from "@/components/ui/card"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { ModelViewer } from "@/components/ModelViewer/ModelViewer"
import { projectDownloadUrl, projectPackUrl, projectPreviewUrl, setNameOverride } from "@/api/projectsApi"
import type { TagName } from "@/types"

export interface ProjectNamePreviewProps {
  projectId: number
  name: TagName
  onOverrideChange: (updated: TagName) => void
}

export function ProjectNamePreview({ projectId, name, onOverrideChange }: ProjectNamePreviewProps) {
  const [depthInput, setDepthInput] = useState(
    name.textDepthMmOverride === null ? "" : String(name.textDepthMmOverride)
  )
  const [isSaving, setIsSaving] = useState(false)
  const [refreshKey, setRefreshKey] = useState(0)

  async function applyOverride(value: number | null) {
    setIsSaving(true)
    try {
      const updated = await setNameOverride(projectId, name.id, value)
      onOverrideChange(updated)
      setRefreshKey((k) => k + 1)
    } catch (err) {
      alert(err instanceof Error ? err.message : "Failed to save override")
    } finally {
      setIsSaving(false)
    }
  }

  // STL preview/download are GET endpoints, so appending a cache-busting query param
  // forces three's loader to refetch after an override changes the generated mesh.
  const previewUrl = `${projectPreviewUrl(projectId, name.id)}?v=${refreshKey}`
  const packPreviewUrl = `${projectPackUrl(projectId, name.id)}?v=${refreshKey}`
  const [showPackPreview, setShowPackPreview] = useState(false)

  return (
    <Card>
      <CardHeader>
        <CardTitle>{name.text}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <div className="h-64 rounded-md border overflow-hidden bg-muted">
          <ModelViewer stlUrl={showPackPreview ? packPreviewUrl : previewUrl} />
        </div>
        <Button type="button" size="sm" variant="ghost" onClick={() => setShowPackPreview((v) => !v)}>
          {showPackPreview ? "Show standing tag" : "Show pack (3-in-1)"}
        </Button>
        <div className="flex items-end gap-2">
          <div className="flex flex-col gap-1.5 flex-1">
            <Label htmlFor={`depth-override-${name.id}`}>Text depth override (mm)</Label>
            <Input
              id={`depth-override-${name.id}`}
              type="number"
              step={0.5}
              placeholder="Use project default"
              value={depthInput}
              onChange={(e) => setDepthInput(e.target.value)}
            />
          </div>
          <Button
            type="button"
            size="sm"
            variant="outline"
            disabled={isSaving}
            onClick={() => applyOverride(depthInput === "" ? null : Number(depthInput))}
          >
            Apply
          </Button>
        </div>
      </CardContent>
      <CardFooter className="flex gap-2">
        <Button asChild size="sm">
          <a href={projectDownloadUrl(projectId, name.id)} download={`${name.text}.stl`}>
            Download STL
          </a>
        </Button>
        <Button asChild size="sm" variant="outline">
          <a href={projectPackUrl(projectId, name.id)} download={`${name.text}-pack.stl`}>
            Download pack (3-in-1)
          </a>
        </Button>
      </CardFooter>
    </Card>
  )
}
