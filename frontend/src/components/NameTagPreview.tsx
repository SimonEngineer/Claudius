import { useEffect, useState } from "react"
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from "@/components/ui/card"
import { ModelViewer } from "@/components/ModelViewer/ModelViewer"
import { ExportButton } from "@/components/ExportButton"
import { fetchTagPreviewBlob } from "@/api/modelsApi"
import { useDebouncedValue } from "@/lib/useDebouncedValue"
import type { TagGenerationParams } from "@/types"

export interface NameTagPreviewProps {
  params: TagGenerationParams
}

export function NameTagPreview({ params }: NameTagPreviewProps) {
  const debouncedParams = useDebouncedValue(params, 400)
  const [previewUrl, setPreviewUrl] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    let objectUrl: string | null = null

    fetchTagPreviewBlob(debouncedParams)
      .then((blob) => {
        if (cancelled) return
        objectUrl = URL.createObjectURL(blob)
        setPreviewUrl(objectUrl)
        setError(null)
      })
      .catch((err) => {
        if (cancelled) return
        setError(err instanceof Error ? err.message : "Preview failed")
      })

    return () => {
      cancelled = true
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [debouncedParams])

  return (
    <Card>
      <CardHeader>
        <CardTitle>{params.text || "(unnamed)"}</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="h-64 rounded-md border overflow-hidden bg-muted">
          {error ? (
            <div className="flex h-full items-center justify-center text-sm text-destructive p-4 text-center">
              {error}
            </div>
          ) : previewUrl ? (
            <ModelViewer stlUrl={previewUrl} />
          ) : null}
        </div>
      </CardContent>
      <CardFooter>
        <ExportButton params={params} />
      </CardFooter>
    </Card>
  )
}
