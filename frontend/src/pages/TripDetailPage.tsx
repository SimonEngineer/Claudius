import { useMemo, useState } from 'react'
import { useParams, Link, useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { TripsApi, MediaApi } from '@/api/resources'
import { BookingType, TimelineEntryType, MediaKind, EntityType, TripStatus } from '@/types'
import type { TripStop, Booking } from '@/types'
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs'
import { Card, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Label } from '@/components/ui/label'
import { Checkbox } from '@/components/ui/checkbox'
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger, DialogFooter,
} from '@/components/ui/dialog'
import { LocationMap } from '@/components/LocationMap'
import { useToast, getErrorMessage } from '@/components/ui/toast'
import { ArrowLeft, Plus, Camera, StickyNote, Link2, Trash2, Pencil, ChevronUp, ChevronDown, MapPin, CalendarDays } from 'lucide-react'

const BOOKING_LABEL: Record<number, string> = {
  [BookingType.Flight]: 'Flight', [BookingType.Hotel]: 'Hotel', [BookingType.CarRental]: 'Car rental',
  [BookingType.Ticket]: 'Ticket', [BookingType.Other]: 'Other',
}
const STATUS_LABEL: Record<number, string> = { [TripStatus.Planning]: 'Planning', [TripStatus.Active]: 'Active', [TripStatus.Completed]: 'Completed' }

function computedStatus(startDate?: string | null, endDate?: string | null, fallback: number = TripStatus.Planning): number {
  if (!startDate || !endDate) return fallback
  const today = new Date().toISOString().slice(0, 10)
  if (today < startDate) return TripStatus.Planning
  if (today > endDate) return TripStatus.Completed
  return TripStatus.Active
}

const BOOKING_FIELD_SCHEMA: Record<number, { key: string; label: string }[]> = {
  [BookingType.Flight]: [
    { key: 'airline', label: 'Airline' }, { key: 'flightNumber', label: 'Flight #' },
    { key: 'seat', label: 'Seat' }, { key: 'terminal', label: 'Terminal/Gate' },
  ],
  [BookingType.Hotel]: [
    { key: 'address', label: 'Address' }, { key: 'roomType', label: 'Room type' },
    { key: 'checkIn', label: 'Check-in time' }, { key: 'checkOut', label: 'Check-out time' },
  ],
  [BookingType.CarRental]: [
    { key: 'company', label: 'Company' }, { key: 'carClass', label: 'Car class' },
    { key: 'pickupLocation', label: 'Pickup location' }, { key: 'dropoffLocation', label: 'Dropoff location' },
  ],
  [BookingType.Ticket]: [
    { key: 'venue', label: 'Venue' }, { key: 'seat', label: 'Seat/Section' },
  ],
  [BookingType.Other]: [],
}

function parseDetails(json?: string | null): Record<string, string> {
  if (!json) return {}
  try { return JSON.parse(json) } catch { return { notes: json } }
}

interface DayPlan { date: string; stops: TripStop[]; bookings: Booking[] }

function buildDayPlan(trip: { startDate?: string | null; endDate?: string | null }, stops: TripStop[], bookings: Booking[]): DayPlan[] {
  const dates: string[] = []
  if (stops.some((s) => s.arriveDate || s.departDate) || bookings.some((b) => b.startAt || b.endAt)) {
    for (const s of stops) { if (s.arriveDate) dates.push(s.arriveDate); if (s.departDate) dates.push(s.departDate) }
    for (const b of bookings) { if (b.startAt) dates.push(b.startAt.slice(0, 10)); if (b.endAt) dates.push(b.endAt.slice(0, 10)) }
  }
  if (trip.startDate) dates.push(trip.startDate)
  if (trip.endDate) dates.push(trip.endDate)
  if (dates.length === 0) return []

  const min = dates.reduce((a, b) => (a < b ? a : b))
  const max = dates.reduce((a, b) => (a > b ? a : b))
  const days: DayPlan[] = []
  let cur = new Date(min + 'T00:00:00')
  const end = new Date(max + 'T00:00:00')
  while (cur <= end) {
    const dateStr = cur.toISOString().slice(0, 10)
    const dayStops = stops.filter((s) => {
      const arrive = s.arriveDate ?? s.departDate
      const depart = s.departDate ?? s.arriveDate
      return arrive && depart && dateStr >= arrive && dateStr <= depart
    })
    const dayBookings = bookings.filter((b) => {
      const start = (b.startAt ?? b.endAt)?.slice(0, 10)
      const finish = (b.endAt ?? b.startAt)?.slice(0, 10)
      return start && finish && dateStr >= start && dateStr <= finish
    })
    days.push({ date: dateStr, stops: dayStops, bookings: dayBookings })
    cur = new Date(cur.getTime() + 86400000)
  }
  return days
}

const EMPTY_STOP_FORM = { name: '', lat: '', lng: '', arriveDate: '', departDate: '', isStart: false, isEnd: false, notes: '' }
const EMPTY_BOOKING_FORM: { type: string; title: string; confirmationNumber: string; startAt: string; endAt: string; cost: string; fields: Record<string, string> } =
  { type: String(BookingType.Flight), title: '', confirmationNumber: '', startAt: '', endAt: '', cost: '', fields: {} }

export function TripDetailPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const { showError } = useToast()
  const [tripMode, setTripMode] = useState(false)

  const { data: trip } = useQuery({ queryKey: ['trip', id], queryFn: () => TripsApi.get(id) })
  const { data: stops = [] } = useQuery({ queryKey: ['trip', id, 'stops'], queryFn: () => TripsApi.stops(id) })
  const { data: bookings = [] } = useQuery({ queryKey: ['trip', id, 'bookings'], queryFn: () => TripsApi.bookings(id) })
  const { data: timeline = [] } = useQuery({ queryKey: ['trip', id, 'timeline'], queryFn: () => TripsApi.timeline(id) })
  const { data: packingItems = [] } = useQuery({ queryKey: ['trip', id, 'packing'], queryFn: () => TripsApi.packing(id) })

  const onErr = (err: unknown) => showError(getErrorMessage(err))

  const [tripEditOpen, setTripEditOpen] = useState(false)
  const [tripForm, setTripForm] = useState({ name: '', description: '', startDate: '', endDate: '', status: String(TripStatus.Planning) })
  const updateTrip = useMutation({
    mutationFn: () => TripsApi.update(id, {
      name: tripForm.name, description: tripForm.description || null,
      startDate: tripForm.startDate || null, endDate: tripForm.endDate || null, status: Number(tripForm.status),
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['trip', id] }); setTripEditOpen(false) },
    onError: onErr,
  })
  const removeTrip = useMutation({
    mutationFn: () => TripsApi.remove(id),
    onSuccess: () => navigate('/trips'),
    onError: onErr,
  })

  const [stopOpen, setStopOpen] = useState(false)
  const [editingStopId, setEditingStopId] = useState<string | null>(null)
  const [stopForm, setStopForm] = useState(EMPTY_STOP_FORM)
  const closeStopDialog = () => { setStopOpen(false); setEditingStopId(null); setStopForm(EMPTY_STOP_FORM) }
  const startEditStop = (s: TripStop) => {
    setEditingStopId(s.id)
    setStopForm({
      name: s.name, lat: String(s.lat), lng: String(s.lng), arriveDate: s.arriveDate ?? '', departDate: s.departDate ?? '',
      isStart: s.isStart, isEnd: s.isEnd, notes: s.notes ?? '',
    })
    setStopOpen(true)
  }
  const saveStop = useMutation({
    mutationFn: () => {
      const payload = {
        name: stopForm.name, lat: parseFloat(stopForm.lat), lng: parseFloat(stopForm.lng),
        arriveDate: stopForm.arriveDate || null, departDate: stopForm.departDate || null,
        sortOrder: editingStopId ? stops.find((s) => s.id === editingStopId)!.sortOrder : stops.length,
        isStart: stopForm.isStart, isEnd: stopForm.isEnd, notes: stopForm.notes || null,
        sourceLocationId: editingStopId ? stops.find((s) => s.id === editingStopId)?.sourceLocationId ?? null : null,
      }
      return editingStopId ? TripsApi.updateStop(editingStopId, payload) : TripsApi.addStop(id, payload)
    },
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['trip', id, 'stops'] }); closeStopDialog() },
    onError: onErr,
  })
  const removeStop = useMutation({
    mutationFn: (stopId: string) => TripsApi.removeStop(stopId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['trip', id, 'stops'] }),
    onError: onErr,
  })
  const reorderStops = useMutation({
    mutationFn: (orderedIds: string[]) => TripsApi.reorderStops(id, orderedIds),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['trip', id, 'stops'] }),
    onError: onErr,
  })
  const moveStop = (stopId: string, direction: -1 | 1) => {
    const sorted = [...stops].sort((a, b) => a.sortOrder - b.sortOrder)
    const idx = sorted.findIndex((s) => s.id === stopId)
    const swapIdx = idx + direction
    if (swapIdx < 0 || swapIdx >= sorted.length) return
    const ids = sorted.map((s) => s.id)
    ;[ids[idx], ids[swapIdx]] = [ids[swapIdx], ids[idx]]
    reorderStops.mutate(ids)
  }

  const [bookingOpen, setBookingOpen] = useState(false)
  const [editingBookingId, setEditingBookingId] = useState<string | null>(null)
  const [bookingForm, setBookingForm] = useState(EMPTY_BOOKING_FORM)
  const closeBookingDialog = () => { setBookingOpen(false); setEditingBookingId(null); setBookingForm(EMPTY_BOOKING_FORM) }
  const startEditBooking = (b: Booking) => {
    setEditingBookingId(b.id)
    const details = parseDetails(b.detailsJson)
    setBookingForm({
      type: String(b.type), title: b.title, confirmationNumber: b.confirmationNumber ?? '',
      startAt: b.startAt ?? '', endAt: b.endAt ?? '', cost: b.cost != null ? String(b.cost) : '', fields: details,
    })
    setBookingOpen(true)
  }
  const saveBooking = useMutation({
    mutationFn: () => {
      const payload = {
        type: Number(bookingForm.type), title: bookingForm.title, confirmationNumber: bookingForm.confirmationNumber || null,
        startAt: bookingForm.startAt || null, endAt: bookingForm.endAt || null, lat: null, lng: null,
        detailsJson: Object.keys(bookingForm.fields).length ? JSON.stringify(bookingForm.fields) : null,
        cost: bookingForm.cost ? parseFloat(bookingForm.cost) : null,
      }
      return editingBookingId ? TripsApi.updateBooking(editingBookingId, payload) : TripsApi.addBooking(id, payload)
    },
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['trip', id, 'bookings'] }); closeBookingDialog() },
    onError: onErr,
  })
  const removeBooking = useMutation({
    mutationFn: (bookingId: string) => TripsApi.removeBooking(bookingId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['trip', id, 'bookings'] }),
    onError: onErr,
  })

  const [packingName, setPackingName] = useState('')
  const addPackingItem = useMutation({
    mutationFn: () => TripsApi.addPackingItem(id, packingName),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['trip', id, 'packing'] }); setPackingName('') },
    onError: onErr,
  })
  const togglePackingItem = useMutation({
    mutationFn: ({ itemId, packed }: { itemId: string; packed: boolean }) => TripsApi.togglePackingItem(itemId, packed),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['trip', id, 'packing'] }),
    onError: onErr,
  })
  const removePackingItem = useMutation({
    mutationFn: (itemId: string) => TripsApi.removePackingItem(itemId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['trip', id, 'packing'] }),
    onError: onErr,
  })

  const [journalText, setJournalText] = useState('')
  const [geoStatus, setGeoStatus] = useState<'idle' | 'locating' | 'found' | 'unavailable'>('idle')
  const addNoteEntry = useMutation({
    mutationFn: () => TripsApi.addTimelineEntry(id, { type: TimelineEntryType.Note, content: journalText }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['trip', id, 'timeline'] }); setJournalText('') },
    onError: onErr,
  })
  const addUrlEntry = useMutation({
    mutationFn: (url: string) => TripsApi.addTimelineEntry(id, { type: TimelineEntryType.Url, content: url }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['trip', id, 'timeline'] }),
    onError: onErr,
  })
  const addMediaEntry = useMutation({
    mutationFn: async (file: File) => {
      const { url } = await MediaApi.upload(file)
      const isVideo = file.type.startsWith('video')
      setGeoStatus('locating')
      const pos = await new Promise<GeolocationPosition | null>((resolve) => {
        if (!navigator.geolocation) return resolve(null)
        navigator.geolocation.getCurrentPosition((p) => resolve(p), () => resolve(null), { timeout: 4000 })
      })
      setGeoStatus(pos ? 'found' : 'unavailable')
      const entry = await TripsApi.addTimelineEntry(id, {
        type: isVideo ? TimelineEntryType.Video : TimelineEntryType.Photo,
        lat: pos?.coords.latitude, lng: pos?.coords.longitude,
      })
      await MediaApi.create({
        entityType: EntityType.TimelineEntry, entityId: entry.id,
        kind: isVideo ? MediaKind.Video : MediaKind.Photo, url, capturedAt: new Date().toISOString(),
      })
    },
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['trip', id, 'timeline'] }); setTimeout(() => setGeoStatus('idle'), 2000) },
    onError: onErr,
  })
  const removeTimelineEntry = useMutation({
    mutationFn: (entryId: string) => TripsApi.removeTimelineEntry(entryId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['trip', id, 'timeline'] }),
    onError: onErr,
  })

  const sortedStops = useMemo(() => [...stops].sort((a, b) => a.sortOrder - b.sortOrder), [stops])
  const pins = sortedStops.map((s) => ({ id: s.id, lat: s.lat, lng: s.lng, label: s.name }))
  const dayPlan = useMemo(() => (trip ? buildDayPlan(trip, stops, bookings) : []), [trip, stops, bookings])
  const budgetTotal = bookings.reduce((sum, b) => sum + (b.cost ?? 0), 0)
  const stopNameById = (stopId?: string | null) => stopId ? stops.find((s) => s.id === stopId)?.name : undefined

  if (!trip) return null

  return (
    <div className="flex flex-col gap-4 pb-20">
      <Link to="/trips" className="flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="h-4 w-4" /> Back to trips
      </Link>

      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">{trip.name}</h1>
          <p className="text-muted-foreground">{trip.startDate} → {trip.endDate}</p>
          <span className="mt-1 inline-block rounded-full bg-secondary px-2 py-0.5 text-xs">{STATUS_LABEL[computedStatus(trip.startDate, trip.endDate, trip.status)]}</span>
        </div>
        <div className="flex gap-2">
          <Button variant={tripMode ? 'default' : 'outline'} onClick={() => setTripMode((m) => !m)}>
            {tripMode ? 'On the trip ✓' : 'On the trip?'}
          </Button>
          <Dialog open={tripEditOpen} onOpenChange={(o) => { setTripEditOpen(o); if (o) setTripForm({ name: trip.name, description: trip.description ?? '', startDate: trip.startDate ?? '', endDate: trip.endDate ?? '', status: String(trip.status) }) }}>
            <Button variant="outline" size="icon" onClick={() => setTripEditOpen(true)}><Pencil className="h-4 w-4" /></Button>
            <DialogContent>
              <DialogHeader><DialogTitle>Edit trip</DialogTitle></DialogHeader>
              <div className="flex flex-col gap-3">
                <div><Label>Name</Label><Input value={tripForm.name} onChange={(e) => setTripForm({ ...tripForm, name: e.target.value })} /></div>
                <div><Label>Description</Label><Textarea value={tripForm.description} onChange={(e) => setTripForm({ ...tripForm, description: e.target.value })} /></div>
                <div className="grid grid-cols-2 gap-2">
                  <div><Label>Start date</Label><Input type="date" value={tripForm.startDate} onChange={(e) => setTripForm({ ...tripForm, startDate: e.target.value })} /></div>
                  <div><Label>End date</Label><Input type="date" value={tripForm.endDate} onChange={(e) => setTripForm({ ...tripForm, endDate: e.target.value })} /></div>
                </div>
                <div>
                  <Label>Status</Label>
                  <Select value={tripForm.status} onValueChange={(v) => setTripForm({ ...tripForm, status: v })}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {Object.entries(STATUS_LABEL).map(([v, l]) => <SelectItem key={v} value={v}>{l}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <DialogFooter><Button onClick={() => updateTrip.mutate()} disabled={!tripForm.name}>Save</Button></DialogFooter>
            </DialogContent>
          </Dialog>
          <Button variant="outline" size="icon" onClick={() => { if (confirm(`Delete "${trip.name}"? This removes all stops, bookings and journal entries.`)) removeTrip.mutate() }}>
            <Trash2 className="h-4 w-4" />
          </Button>
        </div>
      </div>

      <Tabs defaultValue={tripMode ? 'journal' : 'itinerary'}>
        <TabsList>
          <TabsTrigger value="itinerary">Day-by-day</TabsTrigger>
          <TabsTrigger value="stops">Stops</TabsTrigger>
          <TabsTrigger value="bookings">Bookings</TabsTrigger>
          <TabsTrigger value="packing">Packing</TabsTrigger>
          <TabsTrigger value="journal">Journal</TabsTrigger>
        </TabsList>

        <TabsContent value="itinerary">
          <div className="flex flex-col gap-2">
            {dayPlan.length === 0 && (
              <p className="text-sm text-muted-foreground">Add dates to stops or bookings (or set trip start/end dates) to see a day-by-day itinerary.</p>
            )}
            {dayPlan.map((day) => (
              <Card key={day.date}>
                <CardContent className="p-3">
                  <div className="flex items-center gap-2 font-medium mb-2">
                    <CalendarDays className="h-4 w-4 text-muted-foreground" />
                    {new Date(day.date + 'T00:00:00').toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric', year: 'numeric' })}
                  </div>
                  {day.stops.length === 0 && day.bookings.length === 0 && (
                    <p className="text-sm text-muted-foreground pl-6">Nothing scheduled.</p>
                  )}
                  <div className="flex flex-col gap-1.5 pl-6">
                    {day.stops.map((s) => (
                      <div key={s.id} className="text-sm flex items-center gap-1.5">
                        <MapPin className="h-3.5 w-3.5 text-muted-foreground" /> {s.name} {s.isStart && '🏁'} {s.isEnd && '🎯'}
                      </div>
                    ))}
                    {day.bookings.map((b) => (
                      <div key={b.id} className="text-sm flex items-center gap-1.5">
                        <span className="text-muted-foreground">{BOOKING_LABEL[b.type]}:</span> {b.title}
                        {b.startAt && <span className="text-muted-foreground">{new Date(b.startAt).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' })}</span>}
                      </div>
                    ))}
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>

        <TabsContent value="stops">
          <div className="flex flex-col gap-3">
            {pins.length > 0 && <LocationMap pins={pins} height={320} />}
            <div className="flex items-center justify-between">
              <h2 className="text-lg font-semibold">Stops</h2>
              <Dialog open={stopOpen} onOpenChange={(o) => (o ? setStopOpen(true) : closeStopDialog())}>
                <DialogTrigger asChild><Button size="sm" onClick={() => { setEditingStopId(null); setStopForm(EMPTY_STOP_FORM) }}><Plus className="h-4 w-4" /> Add stop</Button></DialogTrigger>
                <DialogContent>
                  <DialogHeader><DialogTitle>{editingStopId ? 'Edit stop' : 'Add stop'}</DialogTitle></DialogHeader>
                  <div className="flex flex-col gap-3">
                    <div><Label>Name</Label><Input value={stopForm.name} onChange={(e) => setStopForm({ ...stopForm, name: e.target.value })} /></div>
                    <div>
                      <Label className="flex items-center gap-1"><MapPin className="h-3 w-3" /> Click the map to set location</Label>
                      <LocationMap
                        height={220}
                        pins={stopForm.lat && stopForm.lng ? [{ id: 'pick', lat: parseFloat(stopForm.lat), lng: parseFloat(stopForm.lng), label: stopForm.name || 'New stop' }] : []}
                        center={stopForm.lat && stopForm.lng ? [parseFloat(stopForm.lat), parseFloat(stopForm.lng)] : undefined}
                        zoom={stopForm.lat && stopForm.lng ? 8 : 2}
                        onPick={(lat, lng) => setStopForm({ ...stopForm, lat: lat.toFixed(5), lng: lng.toFixed(5) })}
                      />
                    </div>
                    <div className="grid grid-cols-2 gap-2">
                      <div><Label className="text-xs">Lat</Label><Input value={stopForm.lat} onChange={(e) => setStopForm({ ...stopForm, lat: e.target.value })} /></div>
                      <div><Label className="text-xs">Lng</Label><Input value={stopForm.lng} onChange={(e) => setStopForm({ ...stopForm, lng: e.target.value })} /></div>
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
                  <DialogFooter><Button onClick={() => saveStop.mutate()} disabled={!stopForm.name || !stopForm.lat || !stopForm.lng}>{editingStopId ? 'Save' : 'Add'}</Button></DialogFooter>
                </DialogContent>
              </Dialog>
            </div>
            <div className="flex flex-col gap-2">
              {sortedStops.map((s, i) => (
                <Card key={s.id}>
                  <CardContent className="flex items-center justify-between p-3">
                    <div>
                      <div className="font-medium">{s.name} {s.isStart && '🏁 start'} {s.isEnd && '🎯 end'}</div>
                      <div className="text-sm text-muted-foreground">{s.arriveDate} → {s.departDate}</div>
                      {s.sourceLocationId && (
                        <Link to={`/wishlist/${s.sourceLocationId}`} className="text-xs text-primary underline">From wishlist</Link>
                      )}
                      {s.notes && <p className="text-sm mt-1">{s.notes}</p>}
                    </div>
                    <div className="flex items-center gap-1">
                      <button onClick={() => moveStop(s.id, -1)} disabled={i === 0} className="disabled:opacity-30"><ChevronUp className="h-4 w-4 text-muted-foreground" /></button>
                      <button onClick={() => moveStop(s.id, 1)} disabled={i === sortedStops.length - 1} className="disabled:opacity-30"><ChevronDown className="h-4 w-4 text-muted-foreground" /></button>
                      <button onClick={() => startEditStop(s)}><Pencil className="h-4 w-4 text-muted-foreground" /></button>
                      <button onClick={() => removeStop.mutate(s.id)}><Trash2 className="h-4 w-4 text-muted-foreground" /></button>
                    </div>
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
              <div>
                <h2 className="text-lg font-semibold">Bookings</h2>
                {budgetTotal > 0 && <p className="text-sm text-muted-foreground">Total budget: ${budgetTotal.toFixed(2)}</p>}
              </div>
              <Dialog open={bookingOpen} onOpenChange={(o) => (o ? setBookingOpen(true) : closeBookingDialog())}>
                <DialogTrigger asChild><Button size="sm" onClick={() => { setEditingBookingId(null); setBookingForm(EMPTY_BOOKING_FORM) }}><Plus className="h-4 w-4" /> Add booking</Button></DialogTrigger>
                <DialogContent>
                  <DialogHeader><DialogTitle>{editingBookingId ? 'Edit booking' : 'Add booking'}</DialogTitle></DialogHeader>
                  <div className="flex flex-col gap-3">
                    <div>
                      <Label>Type</Label>
                      <Select value={bookingForm.type} onValueChange={(v) => setBookingForm({ ...bookingForm, type: v, fields: {} })}>
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
                    <div><Label>Cost</Label><Input type="number" step="0.01" value={bookingForm.cost} onChange={(e) => setBookingForm({ ...bookingForm, cost: e.target.value })} placeholder="0.00" /></div>
                    {BOOKING_FIELD_SCHEMA[Number(bookingForm.type)]?.map((f) => (
                      <div key={f.key}>
                        <Label>{f.label}</Label>
                        <Input
                          value={bookingForm.fields[f.key] ?? ''}
                          onChange={(e) => setBookingForm({ ...bookingForm, fields: { ...bookingForm.fields, [f.key]: e.target.value } })}
                        />
                      </div>
                    ))}
                    <div>
                      <Label>Notes</Label>
                      <Textarea value={bookingForm.fields.notes ?? ''} onChange={(e) => setBookingForm({ ...bookingForm, fields: { ...bookingForm.fields, notes: e.target.value } })} />
                    </div>
                  </div>
                  <DialogFooter><Button onClick={() => saveBooking.mutate()} disabled={!bookingForm.title}>{editingBookingId ? 'Save' : 'Add'}</Button></DialogFooter>
                </DialogContent>
              </Dialog>
            </div>
            <div className="flex flex-col gap-2">
              {bookings.map((b) => {
                const details = parseDetails(b.detailsJson)
                return (
                  <Card key={b.id}>
                    <CardContent className="flex items-center justify-between p-3">
                      <div>
                        <div className="font-medium">{BOOKING_LABEL[b.type]} · {b.title}</div>
                        <div className="text-sm text-muted-foreground">{b.startAt} → {b.endAt}</div>
                        {b.confirmationNumber && <div className="text-sm">Conf# {b.confirmationNumber}</div>}
                        {b.cost != null && <div className="text-sm">Cost: ${b.cost.toFixed(2)}</div>}
                        {Object.entries(details).filter(([, v]) => v).map(([k, v]) => (
                          <div key={k} className="text-sm mt-0.5"><span className="text-muted-foreground">{k}:</span> {v}</div>
                        ))}
                      </div>
                      <div className="flex items-center gap-1">
                        <button onClick={() => startEditBooking(b)}><Pencil className="h-4 w-4 text-muted-foreground" /></button>
                        <button onClick={() => removeBooking.mutate(b.id)}><Trash2 className="h-4 w-4 text-muted-foreground" /></button>
                      </div>
                    </CardContent>
                  </Card>
                )
              })}
              {bookings.length === 0 && <p className="text-sm text-muted-foreground">No bookings yet.</p>}
            </div>
          </div>
        </TabsContent>

        <TabsContent value="packing">
          <Card>
            <CardContent className="p-4 flex flex-col gap-3">
              <div className="flex gap-2">
                <Input value={packingName} onChange={(e) => setPackingName(e.target.value)} placeholder="Add a packing item..." />
                <Button onClick={() => addPackingItem.mutate()} disabled={!packingName}>Add</Button>
              </div>
              <div className="flex flex-col gap-2">
                {packingItems.map((p) => (
                  <div key={p.id} className="flex items-center justify-between gap-2 rounded-md border p-2">
                    <label className="flex items-center gap-2 text-sm flex-1">
                      <Checkbox checked={p.isPacked} onCheckedChange={(c) => togglePackingItem.mutate({ itemId: p.id, packed: !!c })} />
                      <span className={p.isPacked ? 'line-through text-muted-foreground' : ''}>{p.name}</span>
                    </label>
                    <button onClick={() => removePackingItem.mutate(p.id)}><Trash2 className="h-4 w-4 text-muted-foreground" /></button>
                  </div>
                ))}
                {packingItems.length === 0 && <p className="text-sm text-muted-foreground">No packing items yet.</p>}
              </div>
            </CardContent>
          </Card>
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
                {geoStatus !== 'idle' && (
                  <p className="text-xs text-muted-foreground flex items-center gap-1">
                    <MapPin className="h-3 w-3" />
                    {geoStatus === 'locating' && 'Capturing location…'}
                    {geoStatus === 'found' && 'Location attached ✓'}
                    {geoStatus === 'unavailable' && 'Location unavailable — saved without geotag'}
                  </p>
                )}
              </CardContent>
            </Card>

            <div className="flex flex-col gap-2">
              {[...timeline].reverse().map((e) => (
                <Card key={e.id}>
                  <CardContent className="p-3 flex items-start justify-between gap-2">
                    <div className="flex-1">
                      <div className="text-xs text-muted-foreground flex items-center gap-2">
                        {new Date(e.capturedAt).toLocaleString()}
                        {stopNameById(e.nearestStopId) && (
                          <span className="flex items-center gap-0.5"><MapPin className="h-3 w-3" /> near {stopNameById(e.nearestStopId)}</span>
                        )}
                      </div>
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
