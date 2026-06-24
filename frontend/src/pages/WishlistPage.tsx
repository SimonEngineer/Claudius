import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { LocationsApi, TagsApi } from '@/api/resources'
import { WishlistStatus, type WishlistLocation } from '@/types'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Label } from '@/components/ui/label'
import { Badge } from '@/components/ui/badge'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger, DialogFooter,
} from '@/components/ui/dialog'
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select'
import { useToast, getErrorMessage } from '@/components/ui/toast'
import { LocationMap } from '@/components/LocationMap'
import { List, Map as MapIcon, Plus, Pencil, Trash2, MapPin } from 'lucide-react'

const EMPTY_FORM = { name: '', description: '', country: '', lat: '', lng: '', status: WishlistStatus.Idea as number }

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

export function WishlistPage() {
  const [view, setView] = useState<'list' | 'map'>('list')
  const [open, setOpen] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [form, setForm] = useState(EMPTY_FORM)
  const [tagFilter, setTagFilter] = useState<string>('all')
  const [statusFilter, setStatusFilter] = useState<string>('all')
  const qc = useQueryClient()
  const { showError } = useToast()
  const onErr = (err: unknown) => showError(getErrorMessage(err))

  const { data: locations = [] } = useQuery({ queryKey: ['locations'], queryFn: LocationsApi.list })
  const { data: tags = [] } = useQuery({ queryKey: ['tags'], queryFn: TagsApi.list })

  const closeDialog = () => { setOpen(false); setEditingId(null); setForm(EMPTY_FORM) }

  const save = useMutation({
    mutationFn: () => {
      const payload = {
        name: form.name,
        description: form.description || null,
        country: form.country || null,
        lat: form.lat ? parseFloat(form.lat) : null,
        lng: form.lng ? parseFloat(form.lng) : null,
        status: form.status,
      }
      return editingId ? LocationsApi.update(editingId, payload) : LocationsApi.create(payload)
    },
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['locations'] }); closeDialog() },
    onError: onErr,
  })

  const remove = useMutation({
    mutationFn: (id: string) => LocationsApi.remove(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['locations'] }),
    onError: onErr,
  })

  const filteredLocations = useMemo(
    () => locations
      .filter((l) => tagFilter === 'all' || l.tags.some((t) => t.id === tagFilter))
      .filter((l) => statusFilter === 'all' || String(l.status) === statusFilter),
    [locations, tagFilter, statusFilter]
  )

  const startEdit = (l: WishlistLocation) => {
    setEditingId(l.id)
    setForm({ name: l.name, description: l.description ?? '', country: l.country ?? '', lat: l.lat?.toString() ?? '', lng: l.lng?.toString() ?? '', status: l.status })
    setOpen(true)
  }

  const pins = useMemo(
    () => filteredLocations.filter((l) => l.lat != null && l.lng != null).map((l) => ({ id: l.id, lat: l.lat!, lng: l.lng!, label: l.name })),
    [filteredLocations]
  )

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Wishlist</h1>
        <div className="flex gap-2">
          <Select value={tagFilter} onValueChange={setTagFilter}>
            <SelectTrigger className="w-40"><SelectValue placeholder="Filter by tag" /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All tags</SelectItem>
              {tags.map((t) => <SelectItem key={t.id} value={t.id}>{t.name}</SelectItem>)}
            </SelectContent>
          </Select>
          <Select value={statusFilter} onValueChange={setStatusFilter}>
            <SelectTrigger className="w-36"><SelectValue placeholder="Filter by status" /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All statuses</SelectItem>
              {Object.entries(STATUS_LABELS).map(([v, label]) => <SelectItem key={v} value={v}>{label}</SelectItem>)}
            </SelectContent>
          </Select>
          <Button variant={view === 'list' ? 'default' : 'outline'} size="icon" onClick={() => setView('list')}>
            <List className="h-4 w-4" />
          </Button>
          <Button variant={view === 'map' ? 'default' : 'outline'} size="icon" onClick={() => setView('map')}>
            <MapIcon className="h-4 w-4" />
          </Button>
          <Dialog open={open} onOpenChange={(o) => (o ? setOpen(true) : closeDialog())}>
            <DialogTrigger asChild>
              <Button onClick={() => { setEditingId(null); setForm(EMPTY_FORM) }}><Plus className="h-4 w-4" /> Add place</Button>
            </DialogTrigger>
            <DialogContent className="max-h-[85vh] overflow-y-auto">
              <DialogHeader><DialogTitle>{editingId ? 'Edit wishlist location' : 'Add wishlist location'}</DialogTitle></DialogHeader>
              <div className="flex flex-col gap-3">
                <div>
                  <Label>Name</Label>
                  <Input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
                </div>
                <div>
                  <Label>Description</Label>
                  <Textarea value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
                </div>
                <div>
                  <Label>Country</Label>
                  <Input value={form.country} onChange={(e) => setForm({ ...form, country: e.target.value })} />
                </div>
                <div>
                  <Label>Status</Label>
                  <Select value={String(form.status)} onValueChange={(v) => setForm({ ...form, status: Number(v) })}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {Object.entries(STATUS_LABELS).map(([v, label]) => <SelectItem key={v} value={v}>{label}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div>
                  <Label className="flex items-center gap-1"><MapPin className="h-3 w-3" /> Click the map to set location (or type coordinates)</Label>
                  <LocationMap
                    height={220}
                    pins={form.lat && form.lng ? [{ id: 'pick', lat: parseFloat(form.lat), lng: parseFloat(form.lng), label: form.name || 'Pinned location' }] : []}
                    center={form.lat && form.lng ? [parseFloat(form.lat), parseFloat(form.lng)] : undefined}
                    zoom={form.lat && form.lng ? 8 : 2}
                    onPick={(lat, lng) => setForm({ ...form, lat: lat.toFixed(5), lng: lng.toFixed(5) })}
                  />
                  <div className="grid grid-cols-2 gap-2 mt-2">
                    <div><Label className="text-xs">Lat</Label><Input value={form.lat} onChange={(e) => setForm({ ...form, lat: e.target.value })} /></div>
                    <div><Label className="text-xs">Lng</Label><Input value={form.lng} onChange={(e) => setForm({ ...form, lng: e.target.value })} /></div>
                  </div>
                </div>
              </div>
              <DialogFooter>
                <Button onClick={() => save.mutate()} disabled={!form.name || save.isPending}>{editingId ? 'Save' : 'Create'}</Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
        </div>
      </div>

      {view === 'map' ? (
        <LocationMap pins={pins} height={500} />
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
          {filteredLocations.map((l) => (
            <Card key={l.id} className="h-full hover:shadow-md transition-shadow relative group">
              <Link to={`/wishlist/${l.id}`}>
                <CardContent className="flex flex-col gap-1 p-4">
                  <div className="font-medium pr-12">{l.name}</div>
                  <div className="text-sm text-muted-foreground">{l.country}</div>
                  {l.description && <p className="text-sm line-clamp-2">{l.description}</p>}
                  <div className="mt-1 flex flex-wrap gap-1">
                    <Badge style={{ backgroundColor: STATUS_COLORS[l.status], color: 'white' }}>{STATUS_LABELS[l.status]}</Badge>
                    {l.tags.map((t) => (
                      <Badge key={t.id} style={{ backgroundColor: t.color, color: 'white' }}>{t.name}</Badge>
                    ))}
                  </div>
                </CardContent>
              </Link>
              <div className="absolute top-2 right-2 flex gap-1 opacity-0 group-hover:opacity-100">
                <button className="rounded-md bg-background/90 p-1.5 border hover:bg-accent" onClick={(e) => { e.preventDefault(); startEdit(l) }}>
                  <Pencil className="h-3.5 w-3.5" />
                </button>
                <button
                  className="rounded-md bg-background/90 p-1.5 border hover:bg-accent"
                  onClick={(e) => { e.preventDefault(); if (confirm(`Delete "${l.name}"?`)) remove.mutate(l.id) }}
                >
                  <Trash2 className="h-3.5 w-3.5" />
                </button>
              </div>
            </Card>
          ))}
          {filteredLocations.length === 0 && <p className="text-sm text-muted-foreground">No wishlist locations match.</p>}
        </div>
      )}
    </div>
  )
}
