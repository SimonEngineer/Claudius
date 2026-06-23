import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { LocationsApi } from '@/api/resources'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Label } from '@/components/ui/label'
import { Badge } from '@/components/ui/badge'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger, DialogFooter,
} from '@/components/ui/dialog'
import { LocationMap } from '@/components/LocationMap'
import { List, Map as MapIcon, Plus } from 'lucide-react'

export function WishlistPage() {
  const [view, setView] = useState<'list' | 'map'>('list')
  const [open, setOpen] = useState(false)
  const [form, setForm] = useState({ name: '', description: '', country: '', lat: '', lng: '' })
  const qc = useQueryClient()

  const { data: locations = [] } = useQuery({ queryKey: ['locations'], queryFn: LocationsApi.list })

  const create = useMutation({
    mutationFn: () =>
      LocationsApi.create({
        name: form.name,
        description: form.description || null,
        country: form.country || null,
        lat: form.lat ? parseFloat(form.lat) : null,
        lng: form.lng ? parseFloat(form.lng) : null,
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['locations'] })
      setOpen(false)
      setForm({ name: '', description: '', country: '', lat: '', lng: '' })
    },
  })

  const pins = useMemo(
    () => locations.filter((l) => l.lat != null && l.lng != null).map((l) => ({ id: l.id, lat: l.lat!, lng: l.lng!, label: l.name })),
    [locations]
  )

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Wishlist</h1>
        <div className="flex gap-2">
          <Button variant={view === 'list' ? 'default' : 'outline'} size="icon" onClick={() => setView('list')}>
            <List className="h-4 w-4" />
          </Button>
          <Button variant={view === 'map' ? 'default' : 'outline'} size="icon" onClick={() => setView('map')}>
            <MapIcon className="h-4 w-4" />
          </Button>
          <Dialog open={open} onOpenChange={setOpen}>
            <DialogTrigger asChild>
              <Button><Plus className="h-4 w-4" /> Add place</Button>
            </DialogTrigger>
            <DialogContent>
              <DialogHeader><DialogTitle>Add wishlist location</DialogTitle></DialogHeader>
              <div className="flex flex-col gap-3">
                <div>
                  <Label>Name</Label>
                  <Input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
                </div>
                <div>
                  <Label>Description</Label>
                  <Textarea value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
                </div>
                <div className="grid grid-cols-3 gap-2">
                  <div>
                    <Label>Country</Label>
                    <Input value={form.country} onChange={(e) => setForm({ ...form, country: e.target.value })} />
                  </div>
                  <div>
                    <Label>Lat</Label>
                    <Input value={form.lat} onChange={(e) => setForm({ ...form, lat: e.target.value })} />
                  </div>
                  <div>
                    <Label>Lng</Label>
                    <Input value={form.lng} onChange={(e) => setForm({ ...form, lng: e.target.value })} />
                  </div>
                </div>
              </div>
              <DialogFooter>
                <Button onClick={() => create.mutate()} disabled={!form.name || create.isPending}>Create</Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
        </div>
      </div>

      {view === 'map' ? (
        <LocationMap pins={pins} height={500} />
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
          {locations.map((l) => (
            <Link to={`/wishlist/${l.id}`} key={l.id}>
              <Card className="h-full hover:shadow-md transition-shadow">
                <CardContent className="flex flex-col gap-1 p-4">
                  <div className="font-medium">{l.name}</div>
                  <div className="text-sm text-muted-foreground">{l.country}</div>
                  {l.description && <p className="text-sm line-clamp-2">{l.description}</p>}
                  <div className="mt-1 flex flex-wrap gap-1">
                    {l.tags.map((t) => (
                      <Badge key={t.id} style={{ backgroundColor: t.color, color: 'white' }}>{t.name}</Badge>
                    ))}
                  </div>
                </CardContent>
              </Card>
            </Link>
          ))}
          {locations.length === 0 && <p className="text-sm text-muted-foreground">No wishlist locations yet.</p>}
        </div>
      )}
    </div>
  )
}
