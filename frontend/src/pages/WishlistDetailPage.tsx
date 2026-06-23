import { useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { LocationsApi } from '@/api/resources'
import { EntityType } from '@/types'
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs'
import { Card, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Label } from '@/components/ui/label'
import { TagPicker } from '@/components/TagPicker'
import { MediaGallery } from '@/components/MediaGallery'
import { LocationMap } from '@/components/LocationMap'
import { Whiteboard } from '@/components/Whiteboard'
import { ArrowLeft, Trash2 } from 'lucide-react'

export function WishlistDetailPage() {
  const { id = '' } = useParams()
  const qc = useQueryClient()

  const { data: location } = useQuery({ queryKey: ['location', id], queryFn: () => LocationsApi.get(id) })
  const { data: notes = [] } = useQuery({ queryKey: ['location', id, 'notes'], queryFn: () => LocationsApi.notes(id) })
  const { data: links = [] } = useQuery({ queryKey: ['location', id, 'links'], queryFn: () => LocationsApi.links(id) })
  const { data: plan } = useQuery({ queryKey: ['location', id, 'plan'], queryFn: () => LocationsApi.getPlan(id) })
  const { data: whiteboard } = useQuery({ queryKey: ['location', id, 'whiteboard'], queryFn: () => LocationsApi.getWhiteboard(id) })

  const [noteText, setNoteText] = useState('')
  const [linkUrl, setLinkUrl] = useState('')
  const [linkLabel, setLinkLabel] = useState('')
  const [planForm, setPlanForm] = useState(plan)

  const addNote = useMutation({
    mutationFn: () => LocationsApi.addNote(id, noteText),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['location', id, 'notes'] }); setNoteText('') },
  })
  const removeNote = useMutation({
    mutationFn: (noteId: string) => LocationsApi.removeNote(noteId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['location', id, 'notes'] }),
  })
  const addLink = useMutation({
    mutationFn: () => LocationsApi.addLink(id, linkUrl, linkLabel || undefined),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['location', id, 'links'] }); setLinkUrl(''); setLinkLabel('') },
  })
  const removeLink = useMutation({
    mutationFn: (linkId: string) => LocationsApi.removeLink(linkId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['location', id, 'links'] }),
  })
  const savePlan = useMutation({
    mutationFn: () => LocationsApi.putPlan(id, planForm ?? {}),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['location', id, 'plan'] }),
  })
  const saveWhiteboard = useMutation({
    mutationFn: (contentJson: string) => LocationsApi.putWhiteboard(id, { contentJson }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['location', id, 'whiteboard'] }),
  })

  if (!location) return null
  const effectivePlan = planForm ?? plan ?? {}

  return (
    <div className="flex flex-col gap-4">
      <Link to="/wishlist" className="flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="h-4 w-4" /> Back to wishlist
      </Link>

      <div>
        <h1 className="text-2xl font-bold">{location.name}</h1>
        <p className="text-muted-foreground">{location.country}</p>
      </div>

      <TagPicker entityType={EntityType.WishlistLocation} entityId={id} selected={location.tags} onChange={() => qc.invalidateQueries({ queryKey: ['location', id] })} />

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
                  <button onClick={() => removeNote.mutate(n.id)}><Trash2 className="h-4 w-4 text-muted-foreground" /></button>
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
                  <button onClick={() => removeLink.mutate(l.id)}><Trash2 className="h-4 w-4 text-muted-foreground" /></button>
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
