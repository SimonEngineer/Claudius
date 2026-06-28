import { useEffect, useMemo, useState } from "react"
import { useNavigate, useParams } from "react-router-dom"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { NameListInput, namesFromText } from "@/components/NameListInput"
import { ShapeSelector } from "@/components/ShapeSelector"
import { ParamsPanel, type PlateParams } from "@/components/ParamsPanel"
import { MountingHolesPanel } from "@/components/MountingHolesPanel"
import { PackSettingsPanel } from "@/components/PackSettingsPanel"
import { NameTagPreview } from "@/components/NameTagPreview"
import { ProjectNamePreview } from "@/components/ProjectNamePreview"
import { DownloadAllButton } from "@/components/DownloadAllButton"
import { DownloadAllPacksButton } from "@/components/DownloadAllPacksButton"
import { createProject, getProject, replaceNames, updateProject } from "@/api/projectsApi"
import {
  DEFAULT_GENERATION_PARAMS,
  DEFAULT_PACK_SETTINGS,
  type MountingHole,
  type PackSettings,
  type ShapeParams,
  type ShapeType,
  type TagName,
} from "@/types"

export function ProjectEditorPage() {
  const { id } = useParams<{ id: string }>()
  const isNew = id === "new" || id === undefined
  const projectId = isNew ? null : Number(id)
  const navigate = useNavigate()

  const [projectName, setProjectName] = useState("Untitled project")
  const [namesText, setNamesText] = useState("Alice\nBob")
  const [shapeType, setShapeType] = useState<ShapeType>(DEFAULT_GENERATION_PARAMS.shapeType)
  const [customSvgBytes, setCustomSvgBytes] = useState<string | null>(null)
  const [plateParams, setPlateParams] = useState<PlateParams>({
    plateWidthMm: DEFAULT_GENERATION_PARAMS.plateWidthMm,
    plateHeightMm: DEFAULT_GENERATION_PARAMS.plateHeightMm,
    plateThicknessMm: DEFAULT_GENERATION_PARAMS.plateThicknessMm,
    textDepthMm: DEFAULT_GENERATION_PARAMS.textDepthMm,
    textMarginLeftMm: DEFAULT_GENERATION_PARAMS.textMarginLeftMm,
    textMarginRightMm: DEFAULT_GENERATION_PARAMS.textMarginRightMm,
    textMarginTopMm: DEFAULT_GENERATION_PARAMS.textMarginTopMm,
    textMarginBottomMm: DEFAULT_GENERATION_PARAMS.textMarginBottomMm,
    textHorizontalAlign: DEFAULT_GENERATION_PARAMS.textHorizontalAlign,
    textVerticalAlign: DEFAULT_GENERATION_PARAMS.textVerticalAlign,
    bevelMm: DEFAULT_GENERATION_PARAMS.bevelMm,
  })
  const [shapeParams, setShapeParams] = useState<ShapeParams>(DEFAULT_GENERATION_PARAMS.shapeParams)
  const [mountingHoles, setMountingHoles] = useState<MountingHole[]>(DEFAULT_GENERATION_PARAMS.mountingHoles)
  const [packSettings, setPackSettings] = useState<PackSettings>(DEFAULT_PACK_SETTINGS)
  const [isSaving, setIsSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(!isNew)
  const [savedNames, setSavedNames] = useState<TagName[]>([])
  const [savedProjectId, setSavedProjectId] = useState<number | null>(projectId)

  useEffect(() => {
    if (isNew || projectId === null) return
    getProject(projectId)
      .then((project) => {
        setProjectName(project.name)
        setNamesText(project.names.map((n) => n.text).join("\n"))
        setShapeType(project.shapeType)
        setCustomSvgBytes(project.customSvgBase64)
        setPlateParams({
          plateWidthMm: project.plateWidthMm,
          plateHeightMm: project.plateHeightMm,
          plateThicknessMm: project.plateThicknessMm,
          textDepthMm: project.textDepthMm,
          textMarginLeftMm: project.textMarginLeftMm,
          textMarginRightMm: project.textMarginRightMm,
          textMarginTopMm: project.textMarginTopMm,
          textMarginBottomMm: project.textMarginBottomMm,
          textHorizontalAlign: project.textHorizontalAlign,
          textVerticalAlign: project.textVerticalAlign,
          bevelMm: project.bevelMm,
        })
        setShapeParams(project.shapeParams)
        setMountingHoles(project.mountingHoles)
        setPackSettings({
          charmPlateWidthMm: project.charmPlateWidthMm,
          charmPlateHeightMm: project.charmPlateHeightMm,
          charmHookOuterRadiusMm: project.charmHookOuterRadiusMm,
          charmHookBandThicknessMm: project.charmHookBandThicknessMm,
          clipPlateWidthMm: project.clipPlateWidthMm,
          clipPlateHeightMm: project.clipPlateHeightMm,
          clipArmLengthMm: project.clipArmLengthMm,
          clipGapMm: project.clipGapMm,
          clipArmThicknessMm: project.clipArmThicknessMm,
        })
        setSavedNames(project.names)
        setSavedProjectId(project.id)
      })
      .catch((err) => setError(err instanceof Error ? err.message : "Failed to load project"))
      .finally(() => setLoading(false))
  }, [isNew, projectId])

  const names = useMemo(() => namesFromText(namesText), [namesText])

  async function handleSave() {
    setIsSaving(true)
    setError(null)
    try {
      const payload = {
        name: projectName,
        shapeType,
        shapeParams,
        customSvgBase64: customSvgBytes,
        fontFamilyOrPath: DEFAULT_GENERATION_PARAMS.fontFamilyOrPath,
        mountingHoles,
        ...plateParams,
        ...packSettings,
      }
      const saved = projectId === null ? await createProject(payload) : await updateProject(projectId, payload)
      const updatedNames = await replaceNames(saved.id, names)
      setSavedNames(updatedNames)
      setSavedProjectId(saved.id)
      if (projectId === null) navigate(`/projects/${saved.id}`)
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to save project")
    } finally {
      setIsSaving(false)
    }
  }

  if (loading) {
    return <div className="p-6 text-sm text-muted-foreground">Loading project…</div>
  }

  return (
    <div className="min-h-svh bg-background p-6">
      <div className="flex items-center justify-between mb-4">
        <Button variant="outline" size="sm" onClick={() => navigate("/")}>
          Back to projects
        </Button>
        <Button onClick={handleSave} disabled={isSaving}>
          {isSaving ? "Saving…" : "Save project"}
        </Button>
      </div>

      {error && <p className="text-sm text-destructive mb-4">{error}</p>}

      <div className="grid grid-cols-1 lg:grid-cols-[320px_1fr] gap-6">
        <div className="flex flex-col gap-4">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="project-name">Project name</Label>
            <Input id="project-name" value={projectName} onChange={(e) => setProjectName(e.target.value)} />
          </div>
          <NameListInput value={namesText} onChange={setNamesText} />
          <ShapeSelector
            shapeType={shapeType}
            onShapeTypeChange={setShapeType}
            customSvgBytes={customSvgBytes}
            onCustomSvgBytesChange={setCustomSvgBytes}
          />
          <ParamsPanel
            plateParams={plateParams}
            onPlateParamsChange={setPlateParams}
            shapeType={shapeType}
            shapeParams={shapeParams}
            onShapeParamsChange={setShapeParams}
          />
          <MountingHolesPanel holes={mountingHoles} onChange={setMountingHoles} />
          <PackSettingsPanel value={packSettings} onChange={setPackSettings} />
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-4">
          {names.map((name) => (
            <NameTagPreview
              key={name}
              params={{
                text: name,
                shapeType,
                customSvgBytes,
                shapeParams,
                mountingHoles,
                fontFamilyOrPath: DEFAULT_GENERATION_PARAMS.fontFamilyOrPath,
                ...plateParams,
              }}
            />
          ))}
          {names.length === 0 && (
            <p className="text-sm text-muted-foreground">Enter at least one name to see a preview.</p>
          )}
        </div>
      </div>

      {savedProjectId !== null && savedNames.length > 0 && (
        <div className="mt-8">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-semibold">Saved tags</h2>
            <div className="flex gap-2">
              <DownloadAllButton projectId={savedProjectId} projectName={projectName} names={savedNames} />
              <DownloadAllPacksButton projectId={savedProjectId} projectName={projectName} names={savedNames} />
            </div>
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-4">
            {savedNames.map((name) => (
              <ProjectNamePreview
                key={name.id}
                projectId={savedProjectId}
                name={name}
                onOverrideChange={(updated) =>
                  setSavedNames((current) => current.map((n) => (n.id === updated.id ? updated : n)))
                }
              />
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
