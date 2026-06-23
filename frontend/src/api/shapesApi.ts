const API_BASE = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5050"

export async function uploadSvg(file: File): Promise<string> {
  const formData = new FormData()
  formData.append("file", file)
  const response = await fetch(`${API_BASE}/api/shapes/svg-upload`, {
    method: "POST",
    body: formData,
  })
  if (!response.ok) {
    const body = await response.json().catch(() => null)
    throw new Error(body?.error ?? `SVG upload failed: ${response.status}`)
  }
  const { bytes } = (await response.json()) as { bytes: string }
  return bytes
}
