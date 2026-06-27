import { useState } from 'react'
import { useParams, Link, useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { LocationsApi, TripsApi } from '@/api/resources'
import { EntityType, WishlistStatus } from '@/types'
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs'
import { Card, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Label } from '@/components/ui/label'
import { TagPicker } from '@/components/TagPicker'
import { MediaGallery } from '@/components/MediaGallery'
import { LocationMap } from '@/components/LocationMap'
import { Whiteboard } from '@/components/Whiteboard'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter,
} from '@/components/ui/dialog'
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select'
import { useToast, getErrorMessage } from '@/components/ui/toast'
import { ArrowLeft, Trash2, Pencil, MapPlus } from 'lucide-react'

const STATUS_LABELS: Record<number, string> = {
  [WishlistStatus.Idea]: 'Idea',
  [WishlistStatus.Planned]: 'Planned',
  [WishlistStatus.Booked]: 'Booked',
  [WishlistStatus.Visited]: 'Visited',
}
const STATUS_COLORS: Record<number, string> = {
  [WishlistStatus.Idea]: '#64748b',
  [WishlistStatus.Planned]: '#3b82f6',
  [WishlistStatus.Booked]: '#a855f7',
  [WishlistStatus.Visited]: '#22c55e',
}

export function WishlistDetailPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [editOpen, setEditOpen] = useState(false)
  const [editForm, setEditForm] = useState({ name: '', description: '', country: '', lat: '', lng: '', status: WishlistStatus.Idea as number })
  const { showError, showSuccess } = useToast()
  const onErr = (err: unknown) => showError(getErrorMessage(err))

  const { data: location } = useQuery({ queryKey: ['location', id], queryFn: () => LocationsApi.get(id) })
  const { data: notes = [] } = useQuery({ queryKey: ['location', id, 'notes'], queryFn: () => LocationsApi.notes(id) })
  const { data: links = [] } = useQuery({ queryKey: ['location', id, 'links'], queryFn: () => LocationsApi.links(id) })
  const { data: plan } = useQuery({ queryKey: ['location', id, 'plan'], queryFn: () => LocationsApi.getPlan(id) })
  const { data: whiteboard } = useQuery({ queryKey: ['location', id, 'whiteboard'], queryFn: () => LocationsApi.getWhiteboard(id) })
  const { data: tripLinks = [] } = useQuery({ queryKey: ['location', id, 'trip-links'], queryFn: () => LocationsApi.tripLinks(id) })
  const { data: trips = [] } = useQuery({ queryKey: ['trips'], queryFn: () => TripsApi.list() })

  const [addToTripOpen, setAddToTripOpen] = useState(false)
  const [selectedTripId, setSelectedTripId] = useState('')

  const [noteText, setNoteText] = useState('')
  const [linkUrl, setLinkUrl] = useState('')
  const [linkLabel, setLinkLabel] = useState('')
  const [planForm, setPlanForm] = useState(plan)

  const addNote = useMutation({
    mutationFn: () => LocationsApi.addNote(id, noteText),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['location', id, 'notes'] }); setNoteText('') },
    onError: onErr,
  })
  const removeNote = useMutation({
    mutationFn: (noteId: string) => LocationsApi.removeNote(noteId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['location', id, 'notes'] }),
    onError: onErr,
  })
  const addLink = useMutation({
    mutationFn: () => LocationsApi.addLink(id, linkUrl, linkLabel || undefined),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['location', id, 'links'] }); setLinkUrl(''); setLinkLabel('') },
    onError: onErr,
  })
  const removeLink = useMutation({
    mutationFn: (linkId: string) => LocationsApi.removeLink(linkId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['location', id, 'links'] }),
    onError: onErr,
  })
  const savePlan = useMutation({
    mutationFn: () => LocationsApi.putPlan(id, planForm ?? {}),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['location', id, 'plan'] }),
    onError: onErr,
  })
  const saveWhiteboard = useMutation({
    mutationFn: (contentJson: string) => LocationsApi.putWhiteboard(id, { contentJson }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['location', id, 'whiteboard'] }),
    onError: onErr,
  })
  const updateLocation = useMutation({
    mutationFn: () => LocationsApi.update(id, {
      name: editForm.name, description: editForm.description || null, country: editForm.country || null,
      lat: editForm.lat ? parseFloat(editForm.lat) : null, lng: editForm.lng ? parseFloat(editForm.lng) : null,
      status: editForm.status,
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['location', id] }); setEditOpen(false) },
    onError: onErr,
  })
  const removeLocation = useMutation({
    mutationFn: () => LocationsApi.remove(id),
    onSuccess: () => navigate('/wishlist'),
    onError: onErr,
  })
  const addToTrip = useMutation({
    mutationFn: (tripId: string) => TripsApi.addStop(tripId, {
      name: location!.name, lat: location!.lat ?? 0, lng: location!.lng ?? 0,
      arriveDate: null, departDate: null, sortOrder: 0, isStart: false, isEnd: false,
      notes: null, sourceLocationId: id,
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['location', id, 'trip-links'] })
      setAddToTripOpen(false)
      setSelectedTripId('')
      showSuccess('Added to trip')
    },
    onError: onErr,
  })

  if (!location) return null
  const effectivePlan = planForm ?? plan ?? {}

  return (
    <div className="flex flex-col gap-4">
      <Link to="/wishlist" className="flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="h-4 w-4" /> Back to wishlist
      </Link>

      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-2">
            {location.name}
            <Badge style={{ backgroundColor: STATUS_COLORS[location.status], color: 'white' }}>{STATUS_LABELS[location.status]}</Badge>
          </h1>
          <p className="text-muted-foreground">{location.country}</p>
        </div>
        <div className="flex gap-2">
          <Dialog open={addToTripOpen} onOpenChange={setAddToTripOpen}>
            <Button variant="outline" onClick={() => setAddToTripOpen(true)}><MapPlus className="h-4 w-4 mr-1" /> Add to trip</Button>
            <DialogContent>
              <DialogHeader><DialogTitle>Add to trip</DialogTitle></DialogHeader>
              <div className="flex flex-col gap-3">
                <Label>Trip</Label>
                <Select value={selectedTripId} onValueChange={setSelectedTripId}>
                  <SelectTrigger><SelectValue placeholder="Select a trip..." /></SelectTrigger>
                  <SelectContent>
                    {trips.map((t) => <SelectItem key={t.id} value={t.id}>{t.name}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <DialogFooter>
                <Button onClick={() => addToTrip.mutate(selectedTripId)} disabled={!selectedTripId || addToTrip.isPending}>Add as stop</Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
          <Dialog open={editOpen} onOpenChange={(o) => { setEditOpen(o); if (o) setEditForm({ name: location.name, description: location.description ?? '', country: location.country ?? '', lat: location.lat?.toString() ?? '', lng: location.lng?.toString() ?? '', status: location.status }) }}>
            <Button variant="outline" size="icon" onClick={() => setEditOpen(true)}><Pencil className="h-4 w-4" /></Button>
            <DialogContent className="max-h-[85vh] overflow-y-auto">
              <DialogHeader><DialogTitle>Edit location</DialogTitle></DialogHeader>
              <div className="flex flex-col gap-3">
                <div><Label>Name</Label><Input value={editForm.name} onChange={(e) => setEditForm({ ...editForm, name: e.target.value })} /></div>
                <div><Label>Description</Label><Textarea value={editForm.description} onChange={(e) => setEditForm({ ...editForm, description: e.target.value })} /></div>
                <div><Label>Country</Label><Input value={editForm.country} onChange={(e) => setEditForm({ ...editForm, country: e.target.value })} /></div>
                <div>
                  <Label>Status</Label>
                  <Select value={String(editForm.status)} onValueChange={(v) => setEditForm({ ...editForm, status: Number(v) })}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {Object.entries(STATUS_LABELS).map(([v, label]) => <SelectItem key={v} value={v}>{label}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div>
                  <Label>Click the map to set location</Label>
                  <LocationMap
                    height={220}
                    pins={editForm.lat && editForm.lng ? [{ id: 'pick', lat: parseFloat(editForm.lat), lng: parseFloat(editForm.lng), label: editForm.name }] : []}
                    center={editForm.lat && editForm.lng ? [parseFloat(editForm.lat), parseFloat(editForm.lng)] : undefined}
                    zoom={editForm.lat && editForm.lng ? 8 : 2}
                    onPick={(lat, lng) => setEditForm({ ...editForm, lat: lat.toFixed(5), lng: lng.toFixed(5) })}
                  />
                  <div className="grid grid-cols-2 gap-2 mt-2">
                    <div><Label className="text-xs">Lat</Label><Input value={editForm.lat} onChange={(e) => setEditForm({ ...editForm, lat: e.target.value })} /></div>
                    <div><Label className="text-xs">Lng</Label><Input value={editForm.lng} onChange={(e) => setEditForm({ ...editForm, lng: e.target.value })} /></div>
                  </div>
                </div>
              </div>
              <DialogFooter><Button onClick={() => updateLocation.mutate()} disabled={!editForm.name}>Save</Button></DialogFooter>
            </DialogContent>
          </Dialog>
          <Button variant="outline" size="icon" onClick={() => { if (confirm(`Delete "${location.name}"? This removes all its notes, links, media and plan.`)) removeLocation.mutate() }}>
            <Trash2 className="h-4 w-4" />
          </Button>
        </div>
      </div>

      <TagPicker entityType={EntityType.WishlistLocation} entityId={id} selected={location.tags} onChange={() => qc.invalidateQueries({ queryKey: ['location', id] })} />

      {tripLinks.length > 0 && (
        <div className="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
          <span>Added to:</span>
          {tripLinks.map((tl) => (
            <Link key={tl.stopId} to={`/trips/${tl.tripId}`} className="text-primary underline">{tl.tripName}</Link>
          ))}
        </div>
      )}

      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="notes">Notes</TabsTrigger>
          <TabsTrigger value="links">Links</TabsTrigger>
          <TabsTrigger value="plan">Plan</TabsTrigger>
          <TabsTrigger value="whiteboard">Whiteboard</TabsTrigger>
          <TabsTrigger value="media">Media</TabsTrigger>
        </TabsList>

        <TabsContent value="overview">
          <Card><CardContent className="p-4 flex flex-col gap-3">
            <p>{location.description}</p>
            {location.lat != null && location.lng != null && (
              <LocationMap pins={[{ id: location.id, lat: location.lat, lng: location.lng, label: location.name }]} center={[location.lat, location.lng]} zoom={6} height={300} />
            )}
          </CardContent></Card>
        </TabsContent>

        <TabsContent value="notes">
          <Card><CardContent className="p-4 flex flex-col gap-3">
            <div className="flex gap-2">
              <Textarea value={noteText} onChange={(e) => setNoteText(e.target.value)} placeholder="Add a note..." />
              <Button onClick={() => addNote.mutate()} disabled={!noteText}>Add</Button>
            </div>
            <div className="flex flex-col gap-2">
              {notes.map((n) => (
                <div key={n.id} className="flex items-start justify-between gap-2 rounded-md border p-2">
                  <p className="text-sm">{n.text}</p>
                  <button className="p-1.5 -m-1.5" onClick={() => removeNote.mutate(n.id)}><Trash2 className="h-4 w-4 text-muted-foreground" /></button>
                </div>
              ))}
            </div>
          </CardContent></Card>
        </TabsContent>

        <TabsContent value="links">
          <Card><CardContent className="p-4 flex flex-col gap-3">
            <div className="flex gap-2">
              <Input value={linkLabel} onChange={(e) => setLinkLabel(e.target.value)} placeholder="Label" className="max-w-40" />
              <Input value={linkUrl} onChange={(e) => setLinkUrl(e.target.value)} placeholder="https://..." />
              <Button onClick={() => addLink.mutate()} disabled={!linkUrl}>Add</Button>
            </div>
            <div className="flex flex-col gap-2">
              {links.map((l) => (
                <div key={l.id} className="flex items-center justify-between gap-2 rounded-md border p-2">
                  <a href={l.url} target="_blank" rel="noreferrer" className="text-sm text-primary underline">{l.label || l.url}</a>
                  <button className="p-1.5 -m-1.5" onClick={() => removeLink.mutate(l.id)}><Trash2 className="h-4 w-4 text-muted-foreground" /></button>
                </div>
              ))}
            </div>
          </CardContent></Card>
        </TabsContent>

        <TabsContent value="plan">
          <Card><CardContent className="p-4 flex flex-col gap-3">
            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label>Trip start</Label>
                <Input type="date" value={effectivePlan.dateRangeStart ?? ''} onChange={(e) => setPlanForm({ ...effectivePlan, dateRangeStart: e.target.value })} />
              </div>
              <div>
                <Label>Trip end</Label>
                <Input type="date" value={effectivePlan.dateRangeEnd ?? ''} onChange={(e) => setPlanForm({ ...effectivePlan, dateRangeEnd: e.target.value })} />
              </div>
            </div>
            <div>
              <Label>Transport</Label>
              <Textarea value={effectivePlan.transportDetails ?? ''} onChange={(e) => setPlanForm({ ...effectivePlan, transportDetails: e.target.value })} placeholder="Flights, trains, how to get there..." />
            </div>
            <div>
              <Label>Accommodation</Label>
              <Textarea value={effectivePlan.accommodationDetails ?? ''} onChange={(e) => setPlanForm({ ...effectivePlan, accommodationDetails: e.target.value })} placeholder="Where to stay..." />
            </div>
            <div>
              <Label>Notes</Label>
              <Textarea value={effectivePlan.freeformText ?? ''} onChange={(e) => setPlanForm({ ...effectivePlan, freeformText: e.target.value })} />
            </div>
            <Button className="self-start" onClick={() => savePlan.mutate()} disabled={savePlan.isPending}>Save plan</Button>
          </CardContent></Card>
        </TabsContent>

        <TabsContent value="whiteboard">
          {whiteboard && <Whiteboard contentJson={whiteboard.contentJson} onSave={(c) => saveWhiteboard.mutate(c)} saving={saveWhiteboard.isPending} />}
        </TabsContent>

        <TabsContent value="media">
          <MediaGallery entityType={EntityType.WishlistLocation} entityId={id} />
        </TabsContent>
      </Tabs>
    </div>
  )
}
