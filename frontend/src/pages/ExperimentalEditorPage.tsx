import { useEffect, useMemo, useState } from "react"
import { useNavigate } from "react-router-dom"
import type { Font } from "opentype.js"
import { STLExporter } from "three/examples/jsm/exporters/STLExporter.js"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from "@/components/ui/card"
import { NameListInput, namesFromText } from "@/components/NameListInput"
import { ExperimentalModelViewer } from "@/components/ModelViewer/ExperimentalModelViewer"
import { loadExperimentalFont, buildExperimentalMesh } from "@/experimental/buildMesh"
import { EXPERIMENTAL_SHAPE_TYPES, type ExperimentalShapeType } from "@/experimental/shapeOutlines"

interface ExperimentalParams {
  shapeType: ExperimentalShapeType
  plateWidthMm: number
  plateHeightMm: number
  plateThicknessMm: number
  textDepthMm: number
  marginMm: number
  cornerRadiusMm: number
  curveSegments: number
}

const DEFAULT_PARAMS: ExperimentalParams = {
  shapeType: "RoundedRectangle",
  plateWidthMm: 70,
  plateHeightMm: 30,
  plateThicknessMm: 3,
  textDepthMm: 2,
  marginMm: 5,
  cornerRadiusMm: 4,
  curveSegments: 48,
}

function NumberField({
  id,
  label,
  value,
  onChange,
  step = 1,
}: {
  id: string
  label: string
  value: number
  onChange: (value: number) => void
  step?: number
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      <input
        id={id}
        type="number"
        step={step}
        value={value}
        className="flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
        onChange={(e) => {
          const next = e.target.valueAsNumber
          if (!Number.isNaN(next)) onChange(next)
        }}
      />
    </div>
  )
}

function NameTagExperimentalPreview({ name, font, params }: { name: string; font: Font; params: ExperimentalParams }) {
  const mesh = useMemo(() => buildExperimentalMesh(font, name, params), [font, name, params])

  function handleDownload() {
    const exporter = new STLExporter()
    const result = exporter.parse(mesh.group, { binary: true }) as DataView
    const blob = new Blob([result.buffer as ArrayBuffer], { type: "model/stl" })
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement("a")
    anchor.href = url
    anchor.download = `${name || "tag"}.stl`
    anchor.click()
    URL.revokeObjectURL(url)
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>{name || "(unnamed)"}</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="h-64 rounded-md border overflow-hidden bg-muted">
          <ExperimentalModelViewer group={mesh.group} />
        </div>
      </CardContent>
      <CardFooter>
        <Button variant="outline" size="sm" onClick={handleDownload}>
          Download STL
        </Button>
      </CardFooter>
    </Card>
  )
}

/**
 * Side-by-side experimental page that builds meshes entirely in the browser
 * (opentype.js glyphs + three.js ExtrudeGeometry) on every keystroke, no
 * debounce, no backend round trip -- purely to compare live-preview
 * responsiveness against the backend-driven ProjectEditorPage. Unsaved,
 * unpersisted, additive only. Shape scope is intentionally limited to the
 * closed-form presets ported in experimental/shapeOutlines.ts (Rectangle,
 * RoundedRectangle, Circle, Oval); margins are uniform-only and there is no
 * containment-shrink guarantee here -- both are out of scope per the M10 plan.
 */
export function ExperimentalEditorPage() {
  const navigate = useNavigate()
  const [namesText, setNamesText] = useState("Alice\nBob")
  const [params, setParams] = useState<ExperimentalParams>(DEFAULT_PARAMS)
  const [font, setFont] = useState<Font | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    loadExperimentalFont()
      .then(setFont)
      .catch((err) => setError(err instanceof Error ? err.message : "Failed to load font"))
  }, [])

  const names = useMemo(() => namesFromText(namesText), [namesText])

  return (
    <div className="min-h-svh bg-background p-6">
      <div className="flex items-center justify-between mb-4">
        <Button variant="outline" size="sm" onClick={() => navigate("/")}>
          Back to projects
        </Button>
        <p className="text-sm text-muted-foreground">Experimental: frontend-only geometry (no backend round trip)</p>
      </div>

      {error && <p className="text-sm text-destructive mb-4">{error}</p>}

      <div className="grid grid-cols-1 lg:grid-cols-[320px_1fr] gap-6">
        <div className="flex flex-col gap-4">
          <NameListInput value={namesText} onChange={setNamesText} />

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="exp-shape-select">Shape</Label>
            <select
              id="exp-shape-select"
              className="flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              value={params.shapeType}
              onChange={(e) => setParams({ ...params, shapeType: e.target.value as ExperimentalShapeType })}
            >
              {EXPERIMENTAL_SHAPE_TYPES.map((shape) => (
                <option key={shape} value={shape}>
                  {shape}
                </option>
              ))}
            </select>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <NumberField id="exp-width" label="Plate width (mm)" value={params.plateWidthMm} onChange={(v) => setParams({ ...params, plateWidthMm: v })} />
            <NumberField id="exp-height" label="Plate height (mm)" value={params.plateHeightMm} onChange={(v) => setParams({ ...params, plateHeightMm: v })} />
            <NumberField id="exp-thickness" label="Plate thickness (mm)" value={params.plateThicknessMm} onChange={(v) => setParams({ ...params, plateThicknessMm: v })} step={0.5} />
            <NumberField id="exp-depth" label="Text depth (mm)" value={params.textDepthMm} onChange={(v) => setParams({ ...params, textDepthMm: v })} step={0.5} />
            <NumberField id="exp-margin" label="Margin (mm)" value={params.marginMm} onChange={(v) => setParams({ ...params, marginMm: v })} step={0.5} />
            {params.shapeType === "RoundedRectangle" && (
              <NumberField id="exp-corner" label="Corner radius (mm)" value={params.cornerRadiusMm} onChange={(v) => setParams({ ...params, cornerRadiusMm: v })} step={0.5} />
            )}
          </div>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-4">
          {!font && !error && <p className="text-sm text-muted-foreground">Loading font…</p>}
          {font &&
            names.map((name) => <NameTagExperimentalPreview key={name} name={name} font={font} params={params} />)}
          {font && names.length === 0 && (
            <p className="text-sm text-muted-foreground">Enter at least one name to see a preview.</p>
          )}
        </div>
      </div>
    </div>
  )
}
