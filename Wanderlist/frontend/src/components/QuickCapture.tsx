import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { TripsApi, MediaApi } from '@/api/resources'
import { TimelineEntryType, MediaKind, EntityType } from '@/types'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { useToast, getErrorMessage } from '@/components/ui/toast'
import { Camera, StickyNote } from 'lucide-react'

function findActiveTrip(trips: { id: string; name: string; startDate?: string | null; endDate?: string | null }[]) {
  const today = new Date().toISOString().slice(0, 10)
  return trips.find((t) => t.startDate && t.endDate && today >= t.startDate && today <= t.endDate)
}

export function QuickCapture() {
  const qc = useQueryClient()
  const { showError, showSuccess } = useToast()
  const [open, setOpen] = useState(false)
  const [text, setText] = useState('')
  const [locating, setLocating] = useState(false)

  const { data: trips = [] } = useQuery({ queryKey: ['trips'], queryFn: TripsApi.list })
  const trip = findActiveTrip(trips)

  const onErr = (err: unknown) => showError(getErrorMessage(err))
  const onSaved = () => {
    if (trip) qc.invalidateQueries({ queryKey: ['trip', trip.id, 'timeline'] })
    setText('')
    setOpen(false)
    showSuccess('Added to journal')
  }

  const addNote = useMutation({
    mutationFn: () => TripsApi.addTimelineEntry(trip!.id, { type: TimelineEntryType.Note, content: text }),
    onSuccess: onSaved,
    onError: onErr,
  })

  const addMedia = useMutation({
    mutationFn: async (file: File) => {
      const { url } = await MediaApi.upload(file)
      const isVideo = file.type.startsWith('video')
      setLocating(true)
      const pos = await new Promise<GeolocationPosition | null>((resolve) => {
        if (!navigator.geolocation) return resolve(null)
        navigator.geolocation.getCurrentPosition((p) => resolve(p), () => resolve(null), { timeout: 4000 })
      })
      const entry = await TripsApi.addTimelineEntry(trip!.id, {
        type: isVideo ? TimelineEntryType.Video : TimelineEntryType.Photo,
        lat: pos?.coords.latitude, lng: pos?.coords.longitude,
      })
      await MediaApi.create({
        entityType: EntityType.TimelineEntry, entityId: entry.id,
        kind: isVideo ? MediaKind.Video : MediaKind.Photo, url, capturedAt: new Date().toISOString(),
      })
    },
    onSuccess: () => { setLocating(false); onSaved() },
    onError: (err: unknown) => { setLocating(false); onErr(err) },
  })

  if (!trip) return null

  return (
    <>
      <button
        onClick={() => setOpen(true)}
        className="fixed bottom-20 right-4 z-30 flex h-14 w-14 items-center justify-center rounded-full bg-primary text-primary-foreground shadow-lg md:bottom-6"
        aria-label="Quick journal capture"
      >
        <Camera className="h-6 w-6" />
      </button>
      <Dialog open={open} onOpenChange={(o) => { setOpen(o); if (!o) setText('') }}>
        <DialogContent>
          <DialogHeader><DialogTitle>{trip.name} journal</DialogTitle></DialogHeader>
          <div className="flex flex-col gap-3">
            <Textarea
              autoFocus
              value={text}
              onChange={(e) => setText(e.target.value)}
              onKeyDown={(e) => { if (e.key === 'Enter' && !e.shiftKey && text) { e.preventDefault(); addNote.mutate() } }}
              placeholder="What's happening right now? (Enter to post)"
            />
            <label>
              <input type="file" accept="image/*,video/*" capture="environment" className="hidden" onChange={(e) => { const f = e.target.files?.[0]; if (f) addMedia.mutate(f); e.target.value = '' }} />
              <Button variant="outline" className="w-full" disabled={addMedia.isPending} asChild>
                <span><Camera className="h-4 w-4" /> {locating ? 'Capturing location…' : 'Snap a photo/video'}</span>
              </Button>
            </label>
          </div>
          <DialogFooter>
            <Button onClick={() => addNote.mutate()} disabled={!text || addNote.isPending}>
              <StickyNote className="h-4 w-4" /> Post note
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )
}
