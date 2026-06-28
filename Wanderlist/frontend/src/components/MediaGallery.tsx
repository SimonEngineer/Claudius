import { useRef } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { MediaApi } from '@/api/resources'
import { MediaKind, type EntityTypeValue } from '@/types'
import { useToast, getErrorMessage } from '@/components/ui/toast'
import { ImagePlus, Trash2, Video } from 'lucide-react'

interface MediaGalleryProps {
  entityType: EntityTypeValue
  entityId: string
}

export function MediaGallery({ entityType, entityId }: MediaGalleryProps) {
  const qc = useQueryClient()
  const fileInput = useRef<HTMLInputElement>(null)
  const { showError } = useToast()
  const onErr = (err: unknown) => showError(getErrorMessage(err))
  const { data: media = [] } = useQuery({
    queryKey: ['media', entityType, entityId],
    queryFn: () => MediaApi.list(entityType, entityId),
  })

  const upload = useMutation({
    mutationFn: async (file: File) => {
      const { url } = await MediaApi.upload(file)
      const kind = file.type.startsWith('video') ? MediaKind.Video : MediaKind.Photo
      return MediaApi.create({ entityType, entityId, kind, url, capturedAt: new Date().toISOString() })
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['media', entityType, entityId] }),
    onError: onErr,
  })

  const remove = useMutation({
    mutationFn: (id: string) => MediaApi.remove(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['media', entityType, entityId] }),
    onError: onErr,
  })

  return (
    <div className="flex flex-col gap-2">
      <div className="grid grid-cols-3 sm:grid-cols-4 gap-2">
        {media.map((m) => (
          <div key={m.id} className="group relative aspect-square overflow-hidden rounded-md border bg-muted">
            {m.kind === MediaKind.Video ? (
              <video src={m.url} className="h-full w-full object-cover" controls />
            ) : (
              <img src={m.url} alt={m.caption ?? ''} className="h-full w-full object-cover" />
            )}
            <button
              onClick={() => remove.mutate(m.id)}
              className="absolute top-1 right-1 rounded-full bg-black/60 p-1 text-white opacity-0 group-hover:opacity-100"
            >
              <Trash2 className="h-3 w-3" />
            </button>
          </div>
        ))}
        <button
          onClick={() => fileInput.current?.click()}
          className="flex aspect-square flex-col items-center justify-center gap-1 rounded-md border border-dashed text-muted-foreground hover:bg-accent"
        >
          <ImagePlus className="h-5 w-5" />
          <Video className="h-4 w-4" />
          <span className="text-xs">Add</span>
        </button>
      </div>
      <input
        ref={fileInput}
        type="file"
        accept="image/*,video/*"
        capture="environment"
        className="hidden"
        onChange={(e) => {
          const file = e.target.files?.[0]
          if (file) upload.mutate(file)
          e.target.value = ''
        }}
      />
    </div>
  )
}
