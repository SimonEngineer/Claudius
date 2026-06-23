import { useState } from "react"
import { Button } from "@/components/ui/button"
import { downloadTag } from "@/api/modelsApi"
import type { TagGenerationParams } from "@/types"

export interface ExportButtonProps {
  params: TagGenerationParams
}

export function ExportButton({ params }: ExportButtonProps) {
  const [isDownloading, setIsDownloading] = useState(false)

  async function handleClick() {
    setIsDownloading(true)
    try {
      await downloadTag(params, `${params.text || "tag"}.stl`)
    } catch (err) {
      alert(err instanceof Error ? err.message : "Download failed")
    } finally {
      setIsDownloading(false)
    }
  }

  return (
    <Button onClick={handleClick} disabled={isDownloading} size="sm">
      {isDownloading ? "Downloading…" : "Download STL"}
    </Button>
  )
}
