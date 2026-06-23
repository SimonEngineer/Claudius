import { useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { TripsApi, MediaApi } from '@/api/resources'
import { BookingType, TimelineEntryType, MediaKind, EntityType } from '@/types'
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs'
import { Card, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Label } from '@/components/ui/label'
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger, DialogFooter,
} from '@/components/ui/dialog'
import { LocationMap } from '@/components/LocationMap'
import { ArrowLeft, Plus, Camera, StickyNote, Link2, Trash2 } from 'lucide-react'

const BOOKING_LABEL: Record<number, string> = {
  [BookingType.Flight]: 'Flight', [BookingType.Hotel]: 'Hotel', [BookingType.CarRental]: 'Car rental',
  [BookingType.Ticket]: 'Ticket', [BookingType.Other]: 'Other',
}

export function TripDetailPage() {
  const { id = '' } = useParams()
  const qc = useQueryClient()
  const [tripMode, setTripMode] = useState(false)

  const { data: trip } = useQuery({ queryKey: ['trip', id], queryFn: () => TripsApi.get(id) })
  const { data: stops = [] } = useQuery({ queryKey: ['trip', id, 'stops'], queryFn: () => TripsApi.stops(id) })
  const { data: bookings = [] } = useQuery({ queryKey: ['trip', id, 'bookings'], queryFn: () => TripsApi.bookings(id) })
  const { data: timeline = [] } = useQuery({ queryKey: ['trip', id, 'timeline'], queryFn: () => TripsApi.timeline(id) })

  const [stopOpen, setStopOpen] = useState(false)
  const [stopForm, setStopForm] = useState({ name: '', lat: '', lng: '', arriveDate: '', departDate: '', isStart: false, isEnd: false, notes: '' })
  const addStop = useMutation({
    mutationFn: () => TripsApi.addStop(id, {
      name: stopForm.name, lat: parseFloat(stopForm.lat), lng: parseFloat(stopForm.lng),
      arriveDate: stopForm.arriveDate || null, departDate: stopForm.departDate || null,
      sortOrder: stops.length, isStart: stopForm.isStart, isEnd: stopForm.isEnd, notes: stopForm.notes || null,
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['trip', id, 'stops'] }); setStopOpen(false); setStopForm({ name: '', lat: '', lng: '', arriveDate: '', departDate: '', isStart: false, isEnd: false, notes: '' }) },
  })
  const removeStop = useMutation({
    mutationFn: (stopId: string) => TripsApi.removeStop(stopId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['trip', id, 'stops'] }),
  })

  const [bookingOpen, setBookingOpen] = useState(false)
  const [bookingForm, setBookingForm] = useState({ type: String(BookingType.Flight), title: '', confirmationNumber: '', startAt: '', endAt: '', details: '' })
  const addBooking = useMutation({
    mutationFn: () => TripsApi.addBooking(id, {
      type: Number(bookingForm.type), title: bookingForm.title, confirmationNumber: bookingForm.confirmationNumber || null,
      startAt: bookingForm.startAt || null, endAt: bookingForm.endAt || null, lat: null, lng: null, detailsJson: bookingForm.details || null,
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['trip', id, 'bookings'] }); setBookingOpen(false); setBookingForm({ type: String(BookingType.Flight), title: '', confirmationNumber: '', startAt: '', endAt: '', details: '' }) },
  })
  const removeBooking = useMutation({
    mutationFn: (bookingId: string) => TripsApi.removeBooking(bookingId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['trip', id, 'bookings'] }),
  })

  const [journalText, setJournalText] = useState('')
  const addNoteEntry = useMutation({
    mutationFn: () => TripsApi.addTimelineEntry(id, { type: TimelineEntryType.Note, content: journalText }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['trip', id, 'timeline'] }); setJournalText('') },
  })
  const addUrlEntry = useMutation({
    mutationFn: (url: string) => TripsApi.addTimelineEntry(id, { type: TimelineEntryType.Url, content: url }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['trip', id, 'timeline'] }),
  })
  const addMediaEntry = useMutation({
    mutationFn: async (file: File) => {
      const { url } = await MediaApi.upload(file)
      const isVideo = file.type.startsWith('video')
      const pos = await new Promise<GeolocationPosition | null>((resolve) => {
        if (!navigator.geolocation) return resolve(null)
        navigator.geolocation.getCurrentPosition((p) => resolve(p), () => resolve(null), { timeout: 4000 })
      })
      const entry = await TripsApi.addTimelineEntry(id, {
        type: isVideo ? TimelineEntryType.Video : TimelineEntryType.Photo,
        lat: pos?.coords.latitude, lng: pos?.coords.longitude,
      })
      await MediaApi.create({
        entityType: EntityType.TimelineEntry, entityId: entry.id,
        kind: isVideo ? MediaKind.Video : MediaKind.Photo, url, capturedAt: new Date().toISOString(),
      })
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['trip', id, 'timeline'] }),
  })
  const removeTimelineEntry = useMutation({
    mutationFn: (entryId: string) => TripsApi.removeTimelineEntry(entryId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['trip', id, 'timeline'] }),
  })

  if (!trip) return null

  const pins = stops.map((s) => ({ id: s.id, lat: s.lat, lng: s.lng, label: s.name }))

  return (
    <div className="flex flex-col gap-4 pb-20">
      <Link to="/trips" className="flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="h-4 w-4" /> Back to trips
      </Link>

      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">{trip.name}</h1>
          <p className="text-muted-foreground">{trip.startDate} → {trip.endDate}</p>
        </div>
        <Button variant={tripMode ? 'default' : 'outline'} onClick={() => setTripMode((m) => !m)}>
          {tripMode ? 'On the trip ✓' : 'On the trip?'}
        </Button>
      </div>

      <Tabs defaultValue={tripMode ? 'journal' : 'overview'}>
        <TabsList>
          <TabsTrigger value="overview">Itinerary</TabsTrigger>
          <TabsTrigger value="bookings">Bookings</TabsTrigger>
          <TabsTrigger value="journal">Journal</TabsTrigger>
        </TabsList>

        <TabsContent value="overview">
          <div className="flex flex-col gap-3">
            {pins.length > 0 && <LocationMap pins={pins} height={320} />}
            <div className="flex items-center justify-between">
              <h2 className="text-lg font-semibold">Stops</h2>
              <Dialog open={stopOpen} onOpenChange={setStopOpen}>
                <DialogTrigger asChild><Button size="sm"><Plus className="h-4 w-4" /> Add stop</Button></DialogTrigger>
                <DialogContent>
                  <DialogHeader><DialogTitle>Add stop</DialogTitle></DialogHeader>
                  <div className="flex flex-col gap-3">
                    <div><Label>Name</Label><Input value={stopForm.name} onChange={(e) => setStopForm({ ...stopForm, name: e.target.value })} /></div>
                    <div className="grid grid-cols-2 gap-2">
                      <div><Label>Lat</Label><Input value={stopForm.lat} onChange={(e) => setStopForm({ ...stopForm, lat: e.target.value })} /></div>
                      <div><Label>Lng</Label><Input value={stopForm.lng} onChange={(e) => setStopForm({ ...stopForm, lng: e.target.value })} /></div>
                    </div>
                    <div className="grid grid-cols-2 gap-2">
                      <div><Label>Arrive</Label><Input type="date" value={stopForm.arriveDate} onChange={(e) => setStopForm({ ...stopForm, arriveDate: e.target.value })} /></div>
                      <div><Label>Depart</Label><Input type="date" value={stopForm.departDate} onChange={(e) => setStopForm({ ...stopForm, departDate: e.target.value })} /></div>
                    </div>
                    <div className="flex gap-4">
                      <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={stopForm.isStart} onChange={(e) => setStopForm({ ...stopForm, isStart: e.target.checked })} /> Start</label>
                      <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={stopForm.isEnd} onChange={(e) => setStopForm({ ...stopForm, isEnd: e.target.checked })} /> End</label>
                    </div>
                    <div><Label>Notes</Label><Textarea value={stopForm.notes} onChange={(e) => setStopForm({ ...stopForm, notes: e.target.value })} /></div>
                  </div>
                  <DialogFooter><Button onClick={() => addStop.mutate()} disabled={!stopForm.name || !stopForm.lat || !stopForm.lng}>Add</Button></DialogFooter>
                </DialogContent>
              </Dialog>
            </div>
            <div className="flex flex-col gap-2">
              {stops.map((s) => (
                <Card key={s.id}>
                  <CardContent className="flex items-center justify-between p-3">
                    <div>
                      <div className="font-medium">{s.name} {s.isStart && '🏁 start'} {s.isEnd && '🎯 end'}</div>
                      <div className="text-sm text-muted-foreground">{s.arriveDate} → {s.departDate}</div>
                      {s.notes && <p className="text-sm mt-1">{s.notes}</p>}
                    </div>
                    <button onClick={() => removeStop.mutate(s.id)}><Trash2 className="h-4 w-4 text-muted-foreground" /></button>
                  </CardContent>
                </Card>
              ))}
              {stops.length === 0 && <p className="text-sm text-muted-foreground">No stops yet.</p>}
            </div>
          </div>
        </TabsContent>

        <TabsContent value="bookings">
          <div className="flex flex-col gap-3">
            <div className="flex items-center justify-between">
              <h2 className="text-lg font-semibold">Bookings</h2>
              <Dialog open={bookingOpen} onOpenChange={setBookingOpen}>
                <DialogTrigger asChild><Button size="sm"><Plus className="h-4 w-4" /> Add booking</Button></DialogTrigger>
                <DialogContent>
                  <DialogHeader><DialogTitle>Add booking</DialogTitle></DialogHeader>
                  <div className="flex flex-col gap-3">
                    <div>
                      <Label>Type</Label>
                      <Select value={bookingForm.type} onValueChange={(v) => setBookingForm({ ...bookingForm, type: v })}>
                        <SelectTrigger><SelectValue /></SelectTrigger>
                        <SelectContent>
                          {Object.entries(BOOKING_LABEL).map(([v, l]) => <SelectItem key={v} value={v}>{l}</SelectItem>)}
                        </SelectContent>
                      </Select>
                    </div>
                    <div><Label>Title</Label><Input value={bookingForm.title} onChange={(e) => setBookingForm({ ...bookingForm, title: e.target.value })} placeholder="Delta DL123 / Marriott Downtown" /></div>
                    <div><Label>Confirmation #</Label><Input value={bookingForm.confirmationNumber} onChange={(e) => setBookingForm({ ...bookingForm, confirmationNumber: e.target.value })} /></div>
                    <div className="grid grid-cols-2 gap-2">
                      <div><Label>Start</Label><Input type="datetime-local" value={bookingForm.startAt} onChange={(e) => setBookingForm({ ...bookingForm, startAt: e.target.value })} /></div>
                      <div><Label>End</Label><Input type="datetime-local" value={bookingForm.endAt} onChange={(e) => setBookingForm({ ...bookingForm, endAt: e.target.value })} /></div>
                    </div>
                    <div><Label>Details</Label><Textarea value={bookingForm.details} onChange={(e) => setBookingForm({ ...bookingForm, details: e.target.value })} placeholder="Seat, address, car class, etc." /></div>
                  </div>
                  <DialogFooter><Button onClick={() => addBooking.mutate()} disabled={!bookingForm.title}>Add</Button></DialogFooter>
                </DialogContent>
              </Dialog>
            </div>
            <div className="flex flex-col gap-2">
              {bookings.map((b) => (
                <Card key={b.id}>
                  <CardContent className="flex items-center justify-between p-3">
                    <div>
                      <div className="font-medium">{BOOKING_LABEL[b.type]} · {b.title}</div>
                      <div className="text-sm text-muted-foreground">{b.startAt} → {b.endAt}</div>
                      {b.confirmationNumber && <div className="text-sm">Conf# {b.confirmationNumber}</div>}
                      {b.detailsJson && <p className="text-sm mt-1 whitespace-pre-wrap">{b.detailsJson}</p>}
                    </div>
                    <button onClick={() => removeBooking.mutate(b.id)}><Trash2 className="h-4 w-4 text-muted-foreground" /></button>
                  </CardContent>
                </Card>
              ))}
              {bookings.length === 0 && <p className="text-sm text-muted-foreground">No bookings yet.</p>}
            </div>
          </div>
        </TabsContent>

        <TabsContent value="journal">
          <div className="flex flex-col gap-3">
            <Card>
              <CardContent className="p-3 flex flex-col gap-2">
                <Textarea value={journalText} onChange={(e) => setJournalText(e.target.value)} placeholder="What's happening right now?" />
                <div className="flex gap-2">
                  <Button size="sm" onClick={() => addNoteEntry.mutate()} disabled={!journalText}>
                    <StickyNote className="h-4 w-4" /> Post note
                  </Button>
                  <label>
                    <input type="file" accept="image/*,video/*" capture="environment" className="hidden" onChange={(e) => { const f = e.target.files?.[0]; if (f) addMediaEntry.mutate(f); e.target.value = '' }} />
                    <Button size="sm" variant="outline" asChild><span><Camera className="h-4 w-4" /> Photo/Video</span></Button>
                  </label>
                  <Button size="sm" variant="outline" onClick={() => { const url = prompt('URL?'); if (url) addUrlEntry.mutate(url) }}>
                    <Link2 className="h-4 w-4" /> Link
                  </Button>
                </div>
              </CardContent>
            </Card>

            <div className="flex flex-col gap-2">
              {[...timeline].reverse().map((e) => (
                <Card key={e.id}>
                  <CardContent className="p-3 flex items-start justify-between gap-2">
                    <div className="flex-1">
                      <div className="text-xs text-muted-foreground">{new Date(e.capturedAt).toLocaleString()}</div>
                      {e.type === TimelineEntryType.Url ? (
                        <a href={e.content ?? ''} target="_blank" rel="noreferrer" className="text-primary underline">{e.content}</a>
                      ) : e.type === TimelineEntryType.Note ? (
                        <p>{e.content}</p>
                      ) : (
                        <TimelineMedia entryId={e.id} />
                      )}
                    </div>
                    <button onClick={() => removeTimelineEntry.mutate(e.id)}><Trash2 className="h-4 w-4 text-muted-foreground" /></button>
                  </CardContent>
                </Card>
              ))}
              {timeline.length === 0 && <p className="text-sm text-muted-foreground">No journal entries yet.</p>}
            </div>
          </div>
        </TabsContent>
      </Tabs>
    </div>
  )
}

function TimelineMedia({ entryId }: { entryId: string }) {
  const { data: media = [] } = useQuery({ queryKey: ['media', EntityType.TimelineEntry, entryId], queryFn: () => MediaApi.list(EntityType.TimelineEntry, entryId) })
  return (
    <div className="flex gap-2 mt-1">
      {media.map((m) => m.kind === MediaKind.Video ? (
        <video key={m.id} src={m.url} className="h-28 rounded-md" controls />
      ) : (
        <img key={m.id} src={m.url} className="h-28 rounded-md object-cover" />
      ))}
    </div>
  )
}
