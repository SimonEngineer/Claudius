import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { TripsApi } from '@/api/resources'
import { TripStatus } from '@/types'
import type { Trip } from '@/types'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Label } from '@/components/ui/label'
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger, DialogFooter,
} from '@/components/ui/dialog'
import { Plus, Pencil, Trash2 } from 'lucide-react'

const STATUS_LABEL: Record<number, string> = { [TripStatus.Planning]: 'Planning', [TripStatus.Active]: 'Active', [TripStatus.Completed]: 'Completed' }

function computedStatus(t: Trip): number {
  if (!t.startDate || !t.endDate) return t.status
  const today = new Date().toISOString().slice(0, 10)
  if (today < t.startDate) return TripStatus.Planning
  if (today > t.endDate) return TripStatus.Completed
  return TripStatus.Active
}

const EMPTY_FORM = { name: '', description: '', startDate: '', endDate: '', status: String(TripStatus.Planning) }

export function TripsPage() {
  const [open, setOpen] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [form, setForm] = useState(EMPTY_FORM)
  const qc = useQueryClient()
  const { data: trips = [] } = useQuery({ queryKey: ['trips'], queryFn: TripsApi.list })

  const closeDialog = () => { setOpen(false); setEditingId(null); setForm(EMPTY_FORM) }

  const save = useMutation({
    mutationFn: () => {
      const payload = {
        name: form.name, description: form.description || null,
        startDate: form.startDate || null, endDate: form.endDate || null, status: Number(form.status),
      }
      return editingId ? TripsApi.update(editingId, payload) : TripsApi.create(payload)
    },
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['trips'] }); closeDialog() },
  })

  const remove = useMutation({
    mutationFn: (id: string) => TripsApi.remove(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['trips'] }),
  })

  const startEdit = (t: Trip) => {
    setEditingId(t.id)
    setForm({ name: t.name, description: t.description ?? '', startDate: t.startDate ?? '', endDate: t.endDate ?? '', status: String(t.status) })
    setOpen(true)
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Trips</h1>
        <Dialog open={open} onOpenChange={(o) => (o ? setOpen(true) : closeDialog())}>
          <DialogTrigger asChild><Button onClick={() => { setEditingId(null); setForm(EMPTY_FORM) }}><Plus className="h-4 w-4" /> New trip</Button></DialogTrigger>
          <DialogContent>
            <DialogHeader><DialogTitle>{editingId ? 'Edit trip' : 'Plan a new trip'}</DialogTitle></DialogHeader>
            <div className="flex flex-col gap-3">
              <div><Label>Name</Label><Input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} placeholder="USA Roadtrip" /></div>
              <div><Label>Description</Label><Textarea value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></div>
              <div className="grid grid-cols-2 gap-2">
                <div><Label>Start date</Label><Input type="date" value={form.startDate} onChange={(e) => setForm({ ...form, startDate: e.target.value })} /></div>
                <div><Label>End date</Label><Input type="date" value={form.endDate} onChange={(e) => setForm({ ...form, endDate: e.target.value })} /></div>
              </div>
              {editingId && (
                <div>
                  <Label>Status</Label>
                  <Select value={form.status} onValueChange={(v) => setForm({ ...form, status: v })}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {Object.entries(STATUS_LABEL).map(([v, l]) => <SelectItem key={v} value={v}>{l}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
              )}
            </div>
            <DialogFooter><Button onClick={() => save.mutate()} disabled={!form.name}>{editingId ? 'Save' : 'Create'}</Button></DialogFooter>
          </DialogContent>
        </Dialog>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
        {trips.map((t) => (
          <Card key={t.id} className="h-full hover:shadow-md transition-shadow relative group">
            <Link to={`/trips/${t.id}`}>
              <CardContent className="flex flex-col gap-1 p-4">
                <div className="font-medium pr-12">{t.name}</div>
                <div className="text-sm text-muted-foreground">{t.startDate} → {t.endDate}</div>
                <span className="mt-1 inline-block rounded-full bg-secondary px-2 py-0.5 text-xs self-start">{STATUS_LABEL[computedStatus(t)]}</span>
              </CardContent>
            </Link>
            <div className="absolute top-2 right-2 flex gap-1 opacity-0 group-hover:opacity-100">
              <button className="rounded-md bg-background/90 p-1.5 border hover:bg-accent" onClick={() => startEdit(t)}>
                <Pencil className="h-3.5 w-3.5" />
              </button>
              <button
                className="rounded-md bg-background/90 p-1.5 border hover:bg-accent"
                onClick={() => { if (confirm(`Delete "${t.name}"?`)) remove.mutate(t.id) }}
              >
                <Trash2 className="h-3.5 w-3.5" />
              </button>
            </div>
          </Card>
        ))}
        {trips.length === 0 && <p className="text-sm text-muted-foreground">No trips yet.</p>}
      </div>
    </div>
  )
}
