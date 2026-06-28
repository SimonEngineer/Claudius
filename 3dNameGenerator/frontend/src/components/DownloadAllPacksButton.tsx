import { Button } from "@/components/ui/button"
import { projectPacksZipUrl } from "@/api/projectsApi"
import type { TagName } from "@/types"

export interface DownloadAllPacksButtonProps {
  projectId: number
  projectName: string
  names: TagName[]
}

/** Zip of one pre-arranged 3-variant (standing + wine-glass charm + clothes clip) pack STL per name. */
export function DownloadAllPacksButton({ projectId, projectName, names }: DownloadAllPacksButtonProps) {
  function handleClick() {
    const anchor = document.createElement("a")
    anchor.href = projectPacksZipUrl(projectId)
    anchor.download = `${projectName || "name-tags"}-packs.zip`
    anchor.click()
  }

  return (
    <Button variant="outline" size="sm" disabled={names.length === 0} onClick={handleClick}>
      Download all packs (.zip)
    </Button>
  )
}
