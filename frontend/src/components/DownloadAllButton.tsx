import { useState } from "react"
import JSZip from "jszip"
import { Button } from "@/components/ui/button"
import { fetchProjectStlBlob } from "@/api/projectsApi"
import type { TagName } from "@/types"

export interface DownloadAllButtonProps {
  projectId: number
  projectName: string
  names: TagName[]
}

export function DownloadAllButton({ projectId, projectName, names }: DownloadAllButtonProps) {
  const [isZipping, setIsZipping] = useState(false)

  async function handleClick() {
    setIsZipping(true)
    try {
      const zip = new JSZip()
      for (const name of names) {
        const blob = await fetchProjectStlBlob(projectId, name.id)
        zip.file(`${name.text || `name-${name.id}`}.stl`, blob)
      }
      const zipBlob = await zip.generateAsync({ type: "blob" })
      const url = URL.createObjectURL(zipBlob)
      const anchor = document.createElement("a")
      anchor.href = url
      anchor.download = `${projectName || "name-tags"}.zip`
      anchor.click()
      URL.revokeObjectURL(url)
    } catch (err) {
      alert(err instanceof Error ? err.message : "Failed to build zip")
    } finally {
      setIsZipping(false)
    }
  }

  return (
    <Button variant="outline" size="sm" disabled={isZipping || names.length === 0} onClick={handleClick}>
      {isZipping ? "Zipping…" : "Download all as .zip"}
    </Button>
  )
}
