import { Button } from "@/components/ui/button"
import { projectExportZipUrl } from "@/api/projectsApi"
import type { TagName } from "@/types"

export interface DownloadAllButtonProps {
  projectId: number
  projectName: string
  names: TagName[]
}

export function DownloadAllButton({ projectId, projectName, names }: DownloadAllButtonProps) {
  function handleClick() {
    const anchor = document.createElement("a")
    anchor.href = projectExportZipUrl(projectId)
    anchor.download = `${projectName || "name-tags"}.zip`
    anchor.click()
  }

  return (
    <Button variant="outline" size="sm" disabled={names.length === 0} onClick={handleClick}>
      Download all as .zip
    </Button>
  )
}
