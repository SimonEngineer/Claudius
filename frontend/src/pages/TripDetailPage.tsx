import { useEffect, useMemo, useState } from 'react'
import { useParams, Link, useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { TripsApi, MediaApi, FxApi } from '@/api/resources'
import { cn } from '@/lib/utils'
import { BookingType, TimelineEntryType, MediaKind, EntityType, TripStatus, ExpenseCategory, DocumentType } from '@/types'
import type { TripStop, Booking, Expense, TravelDocument } from '@/types'
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
import {
  ArrowLeft, Plus, Camera, StickyNote, Link2, Trash2, Pencil, ChevronUp, ChevronDown, MapPin, CalendarDays,
  Share2, Copy, FileText, ShieldAlert, Users, Download, Clock, Cloud, Printer, FileJson, CopyPlus, Search,
} from 'lucide-react'
import { GeocodeApi } from '@/api/resources'
import type { GeocodeResult } from '@/types'

const BOOKING_LABEL: Record<number, string> = {
  [BookingType.Flight]: 'Flight', [BookingType.Hotel]: 'Hotel', [BookingType.CarRental]: 'Car rental',
  [BookingType.Ticket]: 'Ticket', [BookingType.Other]: 'Other',
}
const STATUS_LABEL: Record<number, string> = { [TripStatus.Planning]: 'Planning', [TripStatus.Active]: 'Active', [TripStatus.Completed]: 'Completed' }
const EXPENSE_CATEGORY_LABEL: Record<number, string> = {
  [ExpenseCategory.Lodging]: 'Lodging', [ExpenseCategory.Transport]: 'Transport',
  [ExpenseCategory.Food]: 'Food', [ExpenseCategory.Activities]: 'Activities', [ExpenseCategory.Other]: 'Other',
}
const DOCUMENT_TYPE_LABEL: Record<number, string> = {
  [DocumentType.Passport]: 'Passport', [DocumentType.Visa]: 'Visa', [DocumentType.Insurance]: 'Insurance',
  [DocumentType.BookingConfirmation]: 'Booking confirmation', [DocumentType.Other]: 'Other',
}
const PACKING_TEMPLATES: Record<string, string[]> = {
  Beach: ['Swimsuit', 'Sunscreen', 'Sunglasses', 'Flip-flops', 'Beach towel'],
  Winter: ['Coat', 'Gloves', 'Scarf', 'Thermal layers', 'Snow boots'],
  Business: ['Laptop + charger', 'Business cards', 'Dress shoes', 'Suit/blazer', 'Notebook'],
  Hiking: ['Hiking boots', 'Backpack', 'Water bottle', 'First aid kit', 'Trail snacks'],
  International: ['Passport', 'Travel adapter', 'Currency/cards', 'Copies of documents', 'Phrasebook/translation app'],
}

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

const WEATHER_CODE_LABEL: Record<number, string> = {
  0: 'Clear sky', 1: 'Mostly clear', 2: 'Partly cloudy', 3: 'Overcast',
  45: 'Fog', 48: 'Fog', 51: 'Light drizzle', 53: 'Drizzle', 55: 'Heavy drizzle',
  61: 'Light rain', 63: 'Rain', 65: 'Heavy rain', 71: 'Light snow', 73: 'Snow', 75: 'Heavy snow',
  80: 'Rain showers', 81: 'Rain showers', 82: 'Violent showers', 95: 'Thunderstorm',
}

function isCheckInSoon(startAt: string): boolean {
  const hoursUntil = (new Date(startAt).getTime() - Date.now()) / 3_600_000
  return hoursUntil > 0 && hoursUntil <= 24
}

interface WeatherDay { date: string; max: number; min: number; code: number }
interface WeatherResult { days: WeatherDay[]; timezone: string }

async function fetchWeather(lat: number, lng: number): Promise<WeatherResult> {
  const url = `https://api.open-meteo.com/v1/forecast?latitude=${lat}&longitude=${lng}&daily=temperature_2m_max,temperature_2m_min,weathercode&timezone=auto&forecast_days=7`
  const res = await fetch(url)
  if (!res.ok) throw new Error('Weather lookup failed')
  const json = await res.json()
  const dates: string[] = json.daily.time
  const days = dates.map((date, i) => ({
    date, max: json.daily.temperature_2m_max[i], min: json.daily.temperature_2m_min[i], code: json.daily.weathercode[i],
  }))
  return { days, timezone: json.timezone }
}

function LocalDestinationTime({ timezone }: { timezone: string }) {
  const [now, setNow] = useState(() => new Date())
  useEffect(() => { const t = setInterval(() => setNow(new Date()), 30_000); return () => clearInterval(t) }, [])
  let formatted: string
  try {
    formatted = new Intl.DateTimeFormat(undefined, { timeZone: timezone, hour: '2-digit', minute: '2-digit', weekday: 'short' }).format(now)
  } catch {
    return null
  }
  return (
    <div className="flex items-center gap-1.5 text-sm text-muted-foreground">
      <Clock className="h-3.5 w-3.5" /> Local time: {formatted}
    </div>
  )
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

const EMPTY_STOP_FORM = { name: '', lat: '', lng: '', arriveDate: '', departDate: '', isStart: false, isEnd: false, notes: '', country: '' }
const EMPTY_BOOKING_FORM: { type: string; title: string; confirmationNumber: string; startAt: string; endAt: string; cost: string; fields: Record<string, string> } =
  { type: String(BookingType.Flight), title: '', confirmationNumber: '', startAt: '', endAt: '', cost: '', fields: {} }
const EMPTY_EXPENSE_FORM = {
  category: String(ExpenseCategory.Other), amount: '', currency: 'USD', date: '', note: '',
  paidByCompanionId: '', splitCompanionIds: [] as string[],
}
const EMPTY_DOCUMENT_FORM = { title: '', docType: String(DocumentType.Passport), expiryDate: '', url: '', notes: '' }

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
  const { data: expenses = [] } = useQuery({ queryKey: ['trip', id, 'expenses'], queryFn: () => TripsApi.expenses(id) })
  const { data: companions = [] } = useQuery({ queryKey: ['trip', id, 'companions'], queryFn: () => TripsApi.companions(id) })
  const { data: documents = [] } = useQuery({ queryKey: ['trip', id, 'documents'], queryFn: () => TripsApi.documents(id) })

  const onErr = (err: unknown) => showError(getErrorMessage(err))

  const [tripEditOpen, setTripEditOpen] = useState(false)
  const [tripForm, setTripForm] = useState({ name: '', description: '', startDate: '', endDate: '', status: String(TripStatus.Planning), budget: '', budgetCurrency: 'USD' })
  const updateTrip = useMutation({
    mutationFn: () => TripsApi.update(id, {
      name: tripForm.name, description: tripForm.description || null,
      startDate: tripForm.startDate || null, endDate: tripForm.endDate || null, status: Number(tripForm.status),
      budget: tripForm.budget ? parseFloat(tripForm.budget) : null, budgetCurrency: tripForm.budgetCurrency || null,
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['trip', id] }); setTripEditOpen(false) },
    onError: onErr,
  })
  const removeTrip = useMutation({
    mutationFn: () => TripsApi.remove(id),
    onSuccess: () => navigate('/trips'),
    onError: onErr,
  })
  const duplicateTrip = useMutation({
    mutationFn: () => TripsApi.duplicate(id),
    onSuccess: (newTrip) => navigate(`/trips/${newTrip.id}`),
    onError: onErr,
  })

  const [stopOpen, setStopOpen] = useState(false)
  const [editingStopId, setEditingStopId] = useState<string | null>(null)
  const [stopForm, setStopForm] = useState(EMPTY_STOP_FORM)
  const closeStopDialog = () => { setStopOpen(false); setEditingStopId(null); setStopForm(EMPTY_STOP_FORM); setPlaceQuery(''); setPlaceResults([]) }
  const startEditStop = (s: TripStop) => {
    setEditingStopId(s.id)
    setStopForm({
      name: s.name, lat: String(s.lat), lng: String(s.lng), arriveDate: s.arriveDate ?? '', departDate: s.departDate ?? '',
      isStart: s.isStart, isEnd: s.isEnd, notes: s.notes ?? '', country: s.country ?? '',
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
        country: stopForm.country || null,
      }
      return editingStopId ? TripsApi.updateStop(editingStopId, payload) : TripsApi.addStop(id, payload)
    },
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['trip', id, 'stops'] }); closeStopDialog() },
    onError: onErr,
  })

  const [placeQuery, setPlaceQuery] = useState('')
  const [placeResults, setPlaceResults] = useState<GeocodeResult[]>([])
  const [placeSearching, setPlaceSearching] = useState(false)
  useEffect(() => {
    if (placeQuery.trim().length < 3) { setPlaceResults([]); return }
    const t = setTimeout(async () => {
      setPlaceSearching(true)
      try { setPlaceResults(await GeocodeApi.search(placeQuery)) } catch { setPlaceResults([]) }
      setPlaceSearching(false)
    }, 500)
    return () => clearTimeout(t)
  }, [placeQuery])
  const pickPlace = (r: GeocodeResult) => {
    setStopForm({ ...stopForm, name: stopForm.name || r.label.split(',')[0], lat: r.lat.toFixed(5), lng: r.lng.toFixed(5), country: r.country ?? stopForm.country })
    setPlaceResults([])
    setPlaceQuery('')
  }
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

  const [expenseOpen, setExpenseOpen] = useState(false)
  const [editingExpenseId, setEditingExpenseId] = useState<string | null>(null)
  const [expenseForm, setExpenseForm] = useState(EMPTY_EXPENSE_FORM)
  const closeExpenseDialog = () => { setExpenseOpen(false); setEditingExpenseId(null); setExpenseForm(EMPTY_EXPENSE_FORM) }
  const startEditExpense = (e: Expense) => {
    setEditingExpenseId(e.id)
    setExpenseForm({
      category: String(e.category), amount: String(e.amount), currency: e.currency, date: e.date, note: e.note ?? '',
      paidByCompanionId: e.paidByCompanionId ?? '', splitCompanionIds: e.splitCompanionIds,
    })
    setExpenseOpen(true)
  }
  const saveExpense = useMutation({
    mutationFn: () => {
      const payload = {
        category: Number(expenseForm.category), amount: parseFloat(expenseForm.amount), currency: expenseForm.currency,
        date: expenseForm.date, note: expenseForm.note || null, bookingId: null,
        paidByCompanionId: expenseForm.paidByCompanionId || null, splitCompanionIds: expenseForm.splitCompanionIds,
      }
      return editingExpenseId ? TripsApi.updateExpense(editingExpenseId, payload) : TripsApi.addExpense(id, payload)
    },
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['trip', id, 'expenses'] }); closeExpenseDialog() },
    onError: onErr,
  })
  const removeExpense = useMutation({
    mutationFn: (expenseId: string) => TripsApi.removeExpense(expenseId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['trip', id, 'expenses'] }),
    onError: onErr,
  })

  const [companionName, setCompanionName] = useState('')
  const addCompanion = useMutation({
    mutationFn: () => TripsApi.addCompanion(id, companionName),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['trip', id, 'companions'] }); setCompanionName('') },
    onError: onErr,
  })
  const removeCompanion = useMutation({
    mutationFn: (companionId: string) => TripsApi.removeCompanion(companionId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['trip', id, 'companions'] }),
    onError: onErr,
  })

  const [documentOpen, setDocumentOpen] = useState(false)
  const [editingDocumentId, setEditingDocumentId] = useState<string | null>(null)
  const [documentForm, setDocumentForm] = useState(EMPTY_DOCUMENT_FORM)
  const closeDocumentDialog = () => { setDocumentOpen(false); setEditingDocumentId(null); setDocumentForm(EMPTY_DOCUMENT_FORM) }
  const startEditDocument = (d: TravelDocument) => {
    setEditingDocumentId(d.id)
    setDocumentForm({ title: d.title, docType: String(d.docType), expiryDate: d.expiryDate ?? '', url: d.url ?? '', notes: d.notes ?? '' })
    setDocumentOpen(true)
  }
  const saveDocument = useMutation({
    mutationFn: () => {
      const payload = {
        title: documentForm.title, docType: Number(documentForm.docType), expiryDate: documentForm.expiryDate || null,
        url: documentForm.url || null, notes: documentForm.notes || null,
      }
      return editingDocumentId ? TripsApi.updateDocument(editingDocumentId, payload) : TripsApi.addDocument(id, payload)
    },
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['trip', id, 'documents'] }); closeDocumentDialog() },
    onError: onErr,
  })
  const removeDocument = useMutation({
    mutationFn: (documentId: string) => TripsApi.removeDocument(documentId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['trip', id, 'documents'] }),
    onError: onErr,
  })

  const [emergencyOpen, setEmergencyOpen] = useState(false)
  const [emergencyText, setEmergencyText] = useState('')
  const saveEmergencyInfo = useMutation({
    mutationFn: () => TripsApi.updateEmergencyInfo(id, emergencyText || null),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['trip', id] }); setEmergencyOpen(false) },
    onError: onErr,
  })

  const [shareOpen, setShareOpen] = useState(false)
  const createShareLink = useMutation({
    mutationFn: () => TripsApi.createShareLink(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['trip', id] }),
    onError: onErr,
  })
  const revokeShareLink = useMutation({
    mutationFn: () => TripsApi.revokeShareLink(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['trip', id] }),
    onError: onErr,
  })
  const [copied, setCopied] = useState(false)
  const [copiedConfId, setCopiedConfId] = useState<string | null>(null)

  const sortedStops = useMemo(() => [...stops].sort((a, b) => a.sortOrder - b.sortOrder), [stops])
  const pins = sortedStops.map((s) => ({ id: s.id, lat: s.lat, lng: s.lng, label: s.name }))
  const overviewPins = useMemo(() => [
    ...sortedStops.map((s) => ({ id: `stop-${s.id}`, lat: s.lat, lng: s.lng, label: `📍 ${s.name}` })),
    ...bookings.filter((b) => b.lat != null && b.lng != null).map((b) => ({ id: `booking-${b.id}`, lat: b.lat!, lng: b.lng!, label: `${BOOKING_LABEL[b.type]}: ${b.title}` })),
    ...timeline.filter((e) => e.lat != null && e.lng != null).map((e) => ({ id: `entry-${e.id}`, lat: e.lat!, lng: e.lng!, label: e.content ?? 'Journal entry' })),
  ], [sortedStops, bookings, timeline])
  const dayPlan = useMemo(() => (trip ? buildDayPlan(trip, stops, bookings) : []), [trip, stops, bookings])
  const budgetTotal = bookings.reduce((sum, b) => sum + (b.cost ?? 0), 0)
  const stopNameById = (stopId?: string | null) => stopId ? stops.find((s) => s.id === stopId)?.name : undefined
  const expenseTotal = expenses.reduce((sum, e) => sum + e.amount, 0)
  const expensesByCategory = useMemo(() => {
    const map = new Map<number, number>()
    for (const e of expenses) map.set(e.category, (map.get(e.category) ?? 0) + e.amount)
    return [...map.entries()].sort((a, b) => b[1] - a[1])
  }, [expenses])

  const foreignCurrencies = useMemo(
    () => [...new Set(expenses.map((e) => e.currency.toUpperCase()).filter((c) => c !== (trip?.budgetCurrency ?? '').toUpperCase()))],
    [expenses, trip?.budgetCurrency]
  )
  const fxRates = useQuery({
    queryKey: ['fx-rates', trip?.budgetCurrency, foreignCurrencies],
    queryFn: async () => {
      const rates: Record<string, number> = {}
      for (const c of foreignCurrencies) rates[c] = await FxApi.getRate(c, trip!.budgetCurrency!)
      return rates
    },
    enabled: !!trip?.budgetCurrency && foreignCurrencies.length > 0,
  })
  const convertedExpenseTotal = useMemo(() => {
    if (!trip?.budgetCurrency) return null
    return expenses.reduce((sum, e) => {
      const cur = e.currency.toUpperCase()
      if (cur === trip.budgetCurrency!.toUpperCase()) return sum + e.amount
      const rate = fxRates.data?.[cur]
      return rate ? sum + e.amount * rate : sum
    }, 0)
  }, [expenses, trip?.budgetCurrency, fxRates.data])

  const settlements = useMemo(() => {
    if (companions.length === 0) return []
    const net = new Map<string, number>()
    for (const c of companions) net.set(c.id, 0)
    net.set('owner', 0)
    for (const e of expenses) {
      const participants = e.splitCompanionIds.length > 0 ? e.splitCompanionIds : null
      if (!participants) continue
      const share = e.amount / participants.length
      const payer = e.paidByCompanionId ?? 'owner'
      net.set(payer, (net.get(payer) ?? 0) + e.amount)
      for (const p of participants) net.set(p, (net.get(p) ?? 0) - share)
    }
    const nameOf = (cid: string) => (cid === 'owner' ? 'You' : companions.find((c) => c.id === cid)?.name ?? '?')
    const debtors = [...net.entries()].filter(([, v]) => v < -0.01).sort((a, b) => a[1] - b[1])
    const creditors = [...net.entries()].filter(([, v]) => v > 0.01).sort((a, b) => b[1] - a[1])
    const results: { from: string; to: string; amount: number }[] = []
    let di = 0, ci = 0
    const debtorsCopy = debtors.map(([k, v]) => ({ k, v: -v }))
    const creditorsCopy = creditors.map(([k, v]) => ({ k, v }))
    while (di < debtorsCopy.length && ci < creditorsCopy.length) {
      const amount = Math.min(debtorsCopy[di].v, creditorsCopy[ci].v)
      if (amount > 0.01) results.push({ from: nameOf(debtorsCopy[di].k), to: nameOf(creditorsCopy[ci].k), amount })
      debtorsCopy[di].v -= amount
      creditorsCopy[ci].v -= amount
      if (debtorsCopy[di].v < 0.01) di++
      if (creditorsCopy[ci].v < 0.01) ci++
    }
    return results
  }, [companions, expenses])

  const firstStop = sortedStops[0]
  const weather = useQuery({
    queryKey: ['weather', firstStop?.lat, firstStop?.lng],
    queryFn: () => fetchWeather(firstStop!.lat, firstStop!.lng),
    enabled: !!firstStop,
    staleTime: 30 * 60_000,
  })

  const exportTripData = () => {
    const data = { trip, stops: sortedStops, bookings, expenses, documents, companions, timeline, packingItems }
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `${trip!.name.replace(/[^a-z0-9]+/gi, '-')}.json`
    a.click()
    URL.revokeObjectURL(url)
  }

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
        <div className="flex gap-2 print:hidden">
          <Button variant={tripMode ? 'default' : 'outline'} onClick={() => setTripMode((m) => !m)}>
            {tripMode ? 'On the trip ✓' : 'On the trip?'}
          </Button>
          <Button variant="outline" size="icon" asChild>
            <a href={TripsApi.calendarUrl(id)} download title="Add bookings to calendar (.ics)"><Download className="h-4 w-4" /></a>
          </Button>
          <Button variant="outline" size="icon" onClick={exportTripData} title="Export trip data as JSON"><FileJson className="h-4 w-4" /></Button>
          <Button variant="outline" size="icon" onClick={() => window.print()} title="Print itinerary"><Printer className="h-4 w-4" /></Button>
          <Button variant="outline" size="icon" onClick={() => duplicateTrip.mutate()} title="Duplicate trip"><CopyPlus className="h-4 w-4" /></Button>
          <Dialog open={emergencyOpen} onOpenChange={(o) => { setEmergencyOpen(o); if (o) setEmergencyText(trip.emergencyInfo ?? '') }}>
            <Button variant="outline" size="icon" onClick={() => setEmergencyOpen(true)} title="Emergency info"><ShieldAlert className="h-4 w-4" /></Button>
            <DialogContent>
              <DialogHeader><DialogTitle>Emergency info</DialogTitle></DialogHeader>
              <div className="flex flex-col gap-2">
                <Label>Embassy contacts, emergency numbers, medical notes, etc.</Label>
                <Textarea value={emergencyText} onChange={(e) => setEmergencyText(e.target.value)} rows={6} placeholder="E.g. Local emergency: 112. US Embassy: +1 555 0100. Allergic to penicillin." />
              </div>
              <DialogFooter><Button onClick={() => saveEmergencyInfo.mutate()}>Save</Button></DialogFooter>
            </DialogContent>
          </Dialog>
          <Dialog open={shareOpen} onOpenChange={setShareOpen}>
            <Button variant="outline" size="icon" onClick={() => setShareOpen(true)} title="Share trip"><Share2 className="h-4 w-4" /></Button>
            <DialogContent>
              <DialogHeader><DialogTitle>Share trip</DialogTitle></DialogHeader>
              <div className="flex flex-col gap-3">
                <p className="text-sm text-muted-foreground">Anyone with this link gets a read-only view of stops, bookings and journal entries. No login required, and costs are never shown.</p>
                {trip.shareSlug ? (
                  <>
                    <div className="flex gap-2">
                      <Input readOnly value={`${window.location.origin}/share/${trip.shareSlug}`} />
                      <Button
                        variant="outline"
                        onClick={() => { navigator.clipboard.writeText(`${window.location.origin}/share/${trip.shareSlug}`); setCopied(true); setTimeout(() => setCopied(false), 1500) }}
                      >
                        {copied ? 'Copied!' : <Copy className="h-4 w-4" />}
                      </Button>
                    </div>
                    <Button variant="outline" onClick={() => revokeShareLink.mutate()}>Stop sharing</Button>
                  </>
                ) : (
                  <Button onClick={() => createShareLink.mutate()}>Create share link</Button>
                )}
              </div>
            </DialogContent>
          </Dialog>
          <Dialog open={tripEditOpen} onOpenChange={(o) => { setTripEditOpen(o); if (o) setTripForm({ name: trip.name, description: trip.description ?? '', startDate: trip.startDate ?? '', endDate: trip.endDate ?? '', status: String(trip.status), budget: trip.budget != null ? String(trip.budget) : '', budgetCurrency: trip.budgetCurrency ?? 'USD' }) }}>
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
                <div className="grid grid-cols-2 gap-2">
                  <div><Label>Budget target</Label><Input type="number" step="0.01" value={tripForm.budget} onChange={(e) => setTripForm({ ...tripForm, budget: e.target.value })} placeholder="0.00" /></div>
                  <div><Label>Currency</Label><Input value={tripForm.budgetCurrency} onChange={(e) => setTripForm({ ...tripForm, budgetCurrency: e.target.value })} /></div>
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

      <Tabs key={tripMode ? 'trip' : 'plan'} defaultValue={tripMode ? 'journal' : 'itinerary'}>
        <TabsList className="print:hidden">
          {!tripMode && (
            <>
              <TabsTrigger value="itinerary">Day-by-day</TabsTrigger>
              <TabsTrigger value="stops">Stops</TabsTrigger>
              <TabsTrigger value="bookings">Bookings</TabsTrigger>
              <TabsTrigger value="budget">Budget</TabsTrigger>
              <TabsTrigger value="documents">Documents</TabsTrigger>
            </>
          )}
          <TabsTrigger value="packing">Packing</TabsTrigger>
          <TabsTrigger value="journal">Journal</TabsTrigger>
        </TabsList>

        <TabsContent value="itinerary">
          <div className="flex flex-col gap-2">
            {overviewPins.length > 0 && (
              <Card>
                <CardContent className="p-3">
                  <div className="font-medium mb-2 text-sm">Trip overview</div>
                  <LocationMap pins={overviewPins} height={280} />
                </CardContent>
              </Card>
            )}
            {firstStop && weather.data && weather.data.days.length > 0 && (
              <Card>
                <CardContent className="p-3">
                  <div className="flex items-center justify-between mb-2">
                    <div className="flex items-center gap-1.5 font-medium text-sm"><Cloud className="h-4 w-4 text-muted-foreground" /> Weather near {firstStop.name}</div>
                    <LocalDestinationTime timezone={weather.data.timezone} />
                  </div>
                  <div className="flex gap-3 overflow-x-auto">
                    {weather.data.days.slice(0, 7).map((d) => (
                      <div key={d.date} className="flex flex-col items-center text-xs shrink-0 min-w-[3.5rem]">
                        <div className="text-muted-foreground">{new Date(d.date + 'T00:00:00').toLocaleDateString(undefined, { weekday: 'short' })}</div>
                        <div className="mt-1">{Math.round(d.max)}° / {Math.round(d.min)}°</div>
                        <div className="text-muted-foreground text-[10px] mt-0.5 text-center">{WEATHER_CODE_LABEL[d.code] ?? ''}</div>
                      </div>
                    ))}
                  </div>
                </CardContent>
              </Card>
            )}
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
                    <div className="relative">
                      <Label className="flex items-center gap-1"><Search className="h-3 w-3" /> Search for a place</Label>
                      <Input
                        value={placeQuery}
                        onChange={(e) => setPlaceQuery(e.target.value)}
                        placeholder="e.g. Lisbon, Portugal"
                      />
                      {placeSearching && <div className="absolute right-2 top-8 text-xs text-muted-foreground">Searching…</div>}
                      {placeResults.length > 0 && (
                        <div className="absolute z-50 mt-1 w-full rounded-md border bg-card shadow-lg max-h-48 overflow-y-auto">
                          {placeResults.map((r, i) => (
                            <button
                              key={i}
                              type="button"
                              onClick={() => pickPlace(r)}
                              className="flex w-full px-3 py-2 text-left text-sm hover:bg-accent"
                            >
                              {r.label}
                            </button>
                          ))}
                        </div>
                      )}
                    </div>
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
                      <button className="p-1.5 -m-1.5 disabled:opacity-30" onClick={() => moveStop(s.id, -1)} disabled={i === 0}><ChevronUp className="h-4 w-4 text-muted-foreground" /></button>
                      <button className="p-1.5 -m-1.5 disabled:opacity-30" onClick={() => moveStop(s.id, 1)} disabled={i === sortedStops.length - 1}><ChevronDown className="h-4 w-4 text-muted-foreground" /></button>
                      <button className="p-1.5 -m-1.5" onClick={() => startEditStop(s)}><Pencil className="h-4 w-4 text-muted-foreground" /></button>
                      <button className="p-1.5 -m-1.5" onClick={() => removeStop.mutate(s.id)}><Trash2 className="h-4 w-4 text-muted-foreground" /></button>
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
                        <div className="font-medium flex items-center gap-2">
                          {BOOKING_LABEL[b.type]} · {b.title}
                          {b.type === BookingType.Flight && b.startAt && isCheckInSoon(b.startAt) && (
                            <span className="inline-flex items-center gap-0.5 rounded-full bg-primary/10 text-primary px-2 py-0.5 text-xs"><Clock className="h-3 w-3" /> Check-in opens soon</span>
                          )}
                        </div>
                        <div className="text-sm text-muted-foreground">{b.startAt} → {b.endAt}</div>
                        {b.confirmationNumber && (
                          <div className="text-sm flex items-center gap-1">
                            Conf# {b.confirmationNumber}
                            <button
                              className="p-1 -m-1"
                              onClick={() => { navigator.clipboard.writeText(b.confirmationNumber!); setCopiedConfId(b.id); setTimeout(() => setCopiedConfId(null), 1200) }}
                            >
                              <Copy className="h-3 w-3 text-muted-foreground" />
                            </button>
                            {copiedConfId === b.id && <span className="text-xs text-muted-foreground">Copied!</span>}
                          </div>
                        )}
                        {b.cost != null && <div className="text-sm">Cost: ${b.cost.toFixed(2)}</div>}
                        {Object.entries(details).filter(([, v]) => v).map(([k, v]) => (
                          <div key={k} className="text-sm mt-0.5"><span className="text-muted-foreground">{k}:</span> {v}</div>
                        ))}
                      </div>
                      <div className="flex items-center gap-1">
                        <button className="p-1.5 -m-1.5" onClick={() => startEditBooking(b)}><Pencil className="h-4 w-4 text-muted-foreground" /></button>
                        <button className="p-1.5 -m-1.5" onClick={() => removeBooking.mutate(b.id)}><Trash2 className="h-4 w-4 text-muted-foreground" /></button>
                      </div>
                    </CardContent>
                  </Card>
                )
              })}
              {bookings.length === 0 && <p className="text-sm text-muted-foreground">No bookings yet.</p>}
            </div>
          </div>
        </TabsContent>

        <TabsContent value="budget">
          <div className="flex flex-col gap-3">
            <Card>
              <CardContent className="p-4 flex flex-col gap-2">
                <div className="flex items-center justify-between">
                  <h2 className="text-lg font-semibold">Spending</h2>
                  <div className="text-right">
                    <div className="text-xl font-bold">{expenseTotal.toFixed(2)} {expenses[0]?.currency ?? trip.budgetCurrency ?? ''}</div>
                    {trip.budget != null && (
                      <div className="text-sm text-muted-foreground">of {trip.budget.toFixed(2)} {trip.budgetCurrency} budget</div>
                    )}
                    {convertedExpenseTotal != null && foreignCurrencies.length > 0 && (
                      <div className="text-sm text-muted-foreground">≈ {convertedExpenseTotal.toFixed(2)} {trip.budgetCurrency} total</div>
                    )}
                  </div>
                </div>
                {trip.budget != null && trip.budget > 0 && (
                  <div className="h-2 w-full rounded-full bg-secondary overflow-hidden">
                    <div
                      className={cn('h-full rounded-full', expenseTotal > trip.budget ? 'bg-destructive' : 'bg-primary')}
                      style={{ width: `${Math.min(100, (expenseTotal / trip.budget) * 100)}%` }}
                    />
                  </div>
                )}
                {expensesByCategory.length > 0 && (
                  <div className="flex flex-col gap-1 mt-2">
                    {expensesByCategory.map(([cat, amount]) => (
                      <div key={cat} className="flex items-center justify-between text-sm">
                        <span className="text-muted-foreground">{EXPENSE_CATEGORY_LABEL[cat]}</span>
                        <span>{amount.toFixed(2)}</span>
                      </div>
                    ))}
                  </div>
                )}
              </CardContent>
            </Card>

            <Card>
              <CardContent className="p-4 flex flex-col gap-3">
                <div className="flex items-center justify-between">
                  <h2 className="text-lg font-semibold flex items-center gap-1.5"><Users className="h-4 w-4" /> Companions</h2>
                </div>
                <div className="flex gap-2">
                  <Input value={companionName} onChange={(e) => setCompanionName(e.target.value)} placeholder="Add a travel companion..." />
                  <Button onClick={() => addCompanion.mutate()} disabled={!companionName}>Add</Button>
                </div>
                {companions.length > 0 && (
                  <div className="flex flex-wrap gap-2">
                    {companions.map((c) => (
                      <span key={c.id} className="inline-flex items-center gap-1 rounded-full bg-secondary px-2.5 py-1 text-sm">
                        {c.name}
                        <button onClick={() => removeCompanion.mutate(c.id)}><Trash2 className="h-3 w-3 text-muted-foreground" /></button>
                      </span>
                    ))}
                  </div>
                )}
                {settlements.length > 0 && (
                  <div className="flex flex-col gap-1 mt-1 text-sm">
                    <div className="font-medium">Who owes whom</div>
                    {settlements.map((s, i) => (
                      <div key={i} className="text-muted-foreground">{s.from} owes {s.to} {s.amount.toFixed(2)} {trip.budgetCurrency ?? ''}</div>
                    ))}
                  </div>
                )}
              </CardContent>
            </Card>

            <div className="flex items-center justify-between">
              <h2 className="text-lg font-semibold">Expenses</h2>
              <Dialog open={expenseOpen} onOpenChange={(o) => (o ? setExpenseOpen(true) : closeExpenseDialog())}>
                <DialogTrigger asChild><Button size="sm" onClick={() => { setEditingExpenseId(null); setExpenseForm(EMPTY_EXPENSE_FORM) }}><Plus className="h-4 w-4" /> Add expense</Button></DialogTrigger>
                <DialogContent>
                  <DialogHeader><DialogTitle>{editingExpenseId ? 'Edit expense' : 'Add expense'}</DialogTitle></DialogHeader>
                  <div className="flex flex-col gap-3">
                    <div>
                      <Label>Category</Label>
                      <Select value={expenseForm.category} onValueChange={(v) => setExpenseForm({ ...expenseForm, category: v })}>
                        <SelectTrigger><SelectValue /></SelectTrigger>
                        <SelectContent>
                          {Object.entries(EXPENSE_CATEGORY_LABEL).map(([v, l]) => <SelectItem key={v} value={v}>{l}</SelectItem>)}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="grid grid-cols-2 gap-2">
                      <div><Label>Amount</Label><Input type="number" step="0.01" value={expenseForm.amount} onChange={(e) => setExpenseForm({ ...expenseForm, amount: e.target.value })} placeholder="0.00" /></div>
                      <div><Label>Currency</Label><Input value={expenseForm.currency} onChange={(e) => setExpenseForm({ ...expenseForm, currency: e.target.value })} /></div>
                    </div>
                    <div><Label>Date</Label><Input type="date" value={expenseForm.date} onChange={(e) => setExpenseForm({ ...expenseForm, date: e.target.value })} /></div>
                    <div><Label>Note</Label><Textarea value={expenseForm.note} onChange={(e) => setExpenseForm({ ...expenseForm, note: e.target.value })} /></div>
                    {companions.length > 0 && (
                      <>
                        <div>
                          <Label>Paid by</Label>
                          <Select value={expenseForm.paidByCompanionId || 'owner'} onValueChange={(v) => setExpenseForm({ ...expenseForm, paidByCompanionId: v === 'owner' ? '' : v })}>
                            <SelectTrigger><SelectValue /></SelectTrigger>
                            <SelectContent>
                              <SelectItem value="owner">You</SelectItem>
                              {companions.map((c) => <SelectItem key={c.id} value={c.id}>{c.name}</SelectItem>)}
                            </SelectContent>
                          </Select>
                        </div>
                        <div>
                          <Label>Split with</Label>
                          <div className="flex flex-col gap-1.5 mt-1">
                            {companions.map((c) => (
                              <label key={c.id} className="flex items-center gap-2 text-sm">
                                <Checkbox
                                  checked={expenseForm.splitCompanionIds.includes(c.id)}
                                  onCheckedChange={(checked) => setExpenseForm({
                                    ...expenseForm,
                                    splitCompanionIds: checked
                                      ? [...expenseForm.splitCompanionIds, c.id]
                                      : expenseForm.splitCompanionIds.filter((x) => x !== c.id),
                                  })}
                                />
                                {c.name}
                              </label>
                            ))}
                          </div>
                        </div>
                      </>
                    )}
                  </div>
                  <DialogFooter><Button onClick={() => saveExpense.mutate()} disabled={!expenseForm.amount || !expenseForm.date}>{editingExpenseId ? 'Save' : 'Add'}</Button></DialogFooter>
                </DialogContent>
              </Dialog>
            </div>
            <div className="flex flex-col gap-2">
              {[...expenses].sort((a, b) => b.date.localeCompare(a.date)).map((e) => (
                <Card key={e.id}>
                  <CardContent className="flex items-center justify-between p-3">
                    <div>
                      <div className="font-medium">{EXPENSE_CATEGORY_LABEL[e.category]} · {e.amount.toFixed(2)} {e.currency}</div>
                      <div className="text-sm text-muted-foreground">{e.date}</div>
                      {e.note && <p className="text-sm mt-1">{e.note}</p>}
                    </div>
                    <div className="flex items-center gap-1">
                      <button className="p-1.5 -m-1.5" onClick={() => startEditExpense(e)}><Pencil className="h-4 w-4 text-muted-foreground" /></button>
                      <button className="p-1.5 -m-1.5" onClick={() => removeExpense.mutate(e.id)}><Trash2 className="h-4 w-4 text-muted-foreground" /></button>
                    </div>
                  </CardContent>
                </Card>
              ))}
              {expenses.length === 0 && <p className="text-sm text-muted-foreground">No expenses yet.</p>}
            </div>
          </div>
        </TabsContent>

        <TabsContent value="documents">
          <div className="flex flex-col gap-3">
            <div className="flex items-center justify-between">
              <h2 className="text-lg font-semibold">Documents</h2>
              <Dialog open={documentOpen} onOpenChange={(o) => (o ? setDocumentOpen(true) : closeDocumentDialog())}>
                <DialogTrigger asChild><Button size="sm" onClick={() => { setEditingDocumentId(null); setDocumentForm(EMPTY_DOCUMENT_FORM) }}><Plus className="h-4 w-4" /> Add document</Button></DialogTrigger>
                <DialogContent>
                  <DialogHeader><DialogTitle>{editingDocumentId ? 'Edit document' : 'Add document'}</DialogTitle></DialogHeader>
                  <div className="flex flex-col gap-3">
                    <div><Label>Title</Label><Input value={documentForm.title} onChange={(e) => setDocumentForm({ ...documentForm, title: e.target.value })} placeholder="Passport, Travel insurance..." /></div>
                    <div>
                      <Label>Type</Label>
                      <Select value={documentForm.docType} onValueChange={(v) => setDocumentForm({ ...documentForm, docType: v })}>
                        <SelectTrigger><SelectValue /></SelectTrigger>
                        <SelectContent>
                          {Object.entries(DOCUMENT_TYPE_LABEL).map(([v, l]) => <SelectItem key={v} value={v}>{l}</SelectItem>)}
                        </SelectContent>
                      </Select>
                    </div>
                    <div><Label>Expiry date</Label><Input type="date" value={documentForm.expiryDate} onChange={(e) => setDocumentForm({ ...documentForm, expiryDate: e.target.value })} /></div>
                    <div><Label>URL (link to scan/copy)</Label><Input value={documentForm.url} onChange={(e) => setDocumentForm({ ...documentForm, url: e.target.value })} /></div>
                    <div><Label>Notes</Label><Textarea value={documentForm.notes} onChange={(e) => setDocumentForm({ ...documentForm, notes: e.target.value })} /></div>
                  </div>
                  <DialogFooter><Button onClick={() => saveDocument.mutate()} disabled={!documentForm.title}>{editingDocumentId ? 'Save' : 'Add'}</Button></DialogFooter>
                </DialogContent>
              </Dialog>
            </div>
            <div className="flex flex-col gap-2">
              {documents.map((d) => {
                const daysLeft = d.expiryDate ? Math.ceil((new Date(d.expiryDate).getTime() - Date.now()) / 86_400_000) : null
                return (
                  <Card key={d.id}>
                    <CardContent className="flex items-center justify-between p-3">
                      <div>
                        <div className="font-medium flex items-center gap-1.5"><FileText className="h-4 w-4 text-muted-foreground" /> {d.title} <span className="text-xs text-muted-foreground">({DOCUMENT_TYPE_LABEL[d.docType]})</span></div>
                        {d.expiryDate && (
                          <div className={cn('text-sm', daysLeft != null && daysLeft <= 90 && 'text-destructive')}>
                            Expires {d.expiryDate}{daysLeft != null && daysLeft >= 0 && ` (in ${daysLeft} days)`}{daysLeft != null && daysLeft < 0 && ' — expired'}
                          </div>
                        )}
                        {d.url && <a href={d.url} target="_blank" rel="noreferrer" className="text-sm text-primary underline">View document</a>}
                        {d.notes && <p className="text-sm mt-1">{d.notes}</p>}
                      </div>
                      <div className="flex items-center gap-1">
                        <button className="p-1.5 -m-1.5" onClick={() => startEditDocument(d)}><Pencil className="h-4 w-4 text-muted-foreground" /></button>
                        <button className="p-1.5 -m-1.5" onClick={() => removeDocument.mutate(d.id)}><Trash2 className="h-4 w-4 text-muted-foreground" /></button>
                      </div>
                    </CardContent>
                  </Card>
                )
              })}
              {documents.length === 0 && <p className="text-sm text-muted-foreground">No documents yet.</p>}
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
              <div className="flex flex-col gap-1.5">
                <Label className="text-xs text-muted-foreground">Quick-add a template</Label>
                <div className="flex flex-wrap gap-2">
                  {Object.entries(PACKING_TEMPLATES).map(([name, items]) => (
                    <Button
                      key={name}
                      size="sm"
                      variant="outline"
                      onClick={async () => { for (const item of items) await TripsApi.addPackingItem(id, item); qc.invalidateQueries({ queryKey: ['trip', id, 'packing'] }) }}
                    >
                      {name}
                    </Button>
                  ))}
                </div>
              </div>
              <div className="flex flex-col gap-2">
                {packingItems.map((p) => (
                  <div key={p.id} className="flex items-center justify-between gap-2 rounded-md border p-2">
                    <label className="flex items-center gap-2 text-sm flex-1">
                      <Checkbox checked={p.isPacked} onCheckedChange={(c) => togglePackingItem.mutate({ itemId: p.id, packed: !!c })} />
                      <span className={p.isPacked ? 'line-through text-muted-foreground' : ''}>{p.name}</span>
                    </label>
                    <button className="p-1.5 -m-1.5" onClick={() => removePackingItem.mutate(p.id)}><Trash2 className="h-4 w-4 text-muted-foreground" /></button>
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
                    <button className="p-1.5 -m-1.5" onClick={() => removeTimelineEntry.mutate(e.id)}><Trash2 className="h-4 w-4 text-muted-foreground" /></button>
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
