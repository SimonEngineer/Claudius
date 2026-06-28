import type { ProjectDetail, ProjectSummary, SaveProjectPayload, TagName } from "@/types"

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5050"

async function requestJson<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    headers: { "Content-Type": "application/json" },
    ...init,
  })
  if (!response.ok) {
    throw new Error(`Request failed: ${response.status} ${await response.text()}`)
  }
  return response.json()
}

export function listProjects(): Promise<ProjectSummary[]> {
  return requestJson("/api/projects")
}

export function getProject(id: number): Promise<ProjectDetail> {
  return requestJson(`/api/projects/${id}`)
}

export function createProject(payload: SaveProjectPayload): Promise<ProjectDetail> {
  return requestJson("/api/projects", { method: "POST", body: JSON.stringify(payload) })
}

export function updateProject(id: number, payload: SaveProjectPayload): Promise<ProjectDetail> {
  return requestJson(`/api/projects/${id}`, { method: "PUT", body: JSON.stringify(payload) })
}

export async function deleteProject(id: number): Promise<void> {
  const response = await fetch(`${API_BASE}/api/projects/${id}`, { method: "DELETE" })
  if (!response.ok) {
    throw new Error(`Delete failed: ${response.status} ${await response.text()}`)
  }
}

export function replaceNames(id: number, names: string[]): Promise<TagName[]> {
  return requestJson(`/api/projects/${id}/names`, { method: "PUT", body: JSON.stringify(names) })
}

export function setNameOverride(
  projectId: number,
  nameId: number,
  textDepthMmOverride: number | null
): Promise<TagName> {
  return requestJson(`/api/projects/${projectId}/names/${nameId}/override`, {
    method: "PUT",
    body: JSON.stringify({ textDepthMmOverride }),
  })
}

export function projectPreviewUrl(projectId: number, nameId: number): string {
  return `${API_BASE}/api/projects/${projectId}/names/${nameId}/preview.stl`
}

export function projectDownloadUrl(projectId: number, nameId: number): string {
  return `${API_BASE}/api/projects/${projectId}/names/${nameId}/download.stl`
}

export async function fetchProjectStlBlob(projectId: number, nameId: number): Promise<Blob> {
  const response = await fetch(projectDownloadUrl(projectId, nameId))
  if (!response.ok) {
    throw new Error(`Download failed: ${response.status} ${await response.text()}`)
  }
  return response.blob()
}

export function projectExportZipUrl(projectId: number, nameIds?: number[]): string {
  const query = nameIds && nameIds.length > 0 ? `?nameIds=${nameIds.join(",")}` : ""
  return `${API_BASE}/api/projects/${projectId}/export.zip${query}`
}

/** A pre-arranged STL with all 3 tag variants (standing, wine-glass charm, clothes clip) for one name. */
export function projectPackUrl(projectId: number, nameId: number): string {
  return `${API_BASE}/api/projects/${projectId}/names/${nameId}/pack.stl`
}

/** Zip of one pre-arranged 3-variant pack STL per name in the project. */
export function projectPacksZipUrl(projectId: number): string {
  return `${API_BASE}/api/projects/${projectId}/packs.zip`
}
