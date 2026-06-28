import type { TagGenerationParams } from "@/types"

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5050"

async function postForStlBlob(path: string, params: TagGenerationParams): Promise<Blob> {
  const response = await fetch(`${API_BASE}${path}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(params),
  })
  if (!response.ok) {
    throw new Error(`Generation request failed: ${response.status} ${await response.text()}`)
  }
  return response.blob()
}

export function fetchTagPreviewBlob(params: TagGenerationParams): Promise<Blob> {
  return postForStlBlob("/api/models/preview.stl", params)
}

export async function downloadTag(params: TagGenerationParams, fileName: string): Promise<void> {
  const blob = await postForStlBlob("/api/models/download.stl", params)
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement("a")
  anchor.href = url
  anchor.download = fileName
  anchor.click()
  URL.revokeObjectURL(url)
}
