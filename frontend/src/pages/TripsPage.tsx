import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { TripsApi } from '@/api/resources'
import { TripStatus } from '@/types'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Label } from '@/components/ui/label'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger, DialogFooter,
} from '@/components/ui/dialog'
import { Plus } from 'lucide-react'

const STATUS_LABEL: Record<number, string> = { [TripStatus.Planning]: 'Planning', [TripStatus.Active]: 'Active', [TripStatus.Completed]: 'Completed' }

export function TripsPage() {
  const [open, setOpen] = useState(false)
  const [form, setForm] = useState({ name: '', description: '', startDate: '', endDate: '' })
  const qc = useQueryClient()
  const { data: trips = [] } = useQuery({ queryKey: ['trips'], queryFn: TripsApi.list })

  const create = useMutation({
    mutationFn: () => TripsApi.create({
      name: form.name, description: form.description || null,
      startDate: form.startDate || null, endDate: form.endDate || null,
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['trips'] }); setOpen(false); setForm({ name: '', description: '', startDate: '', endDate: '' }) },
  })

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Trips</h1>
        <Dialog open={open} onOpenChange={setOpen}>
          <DialogTrigger asChild><Button><Plus className="h-4 w-4" /> New trip</Button></DialogTrigger>
          <DialogContent>
            <DialogHeader><DialogTitle>Plan a new trip</DialogTitle></DialogHeader>
            <div className="flex flex-col gap-3">
              <div><Label>Name</Label><Input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} placeholder="USA Roadtrip" /></div>
              <div><Label>Description</Label><Textarea value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></div>
              <div className="grid grid-cols-2 gap-2">
                <div><Label>Start date</Label><Input type="date" value={form.startDate} onChange={(e) => setForm({ ...form, startDate: e.target.value })} /></div>
                <div><Label>End date</Label><Input type="date" value={form.endDate} onChange={(e) => setForm({ ...form, endDate: e.target.value })} /></div>
              </div>
            </div>
            <DialogFooter><Button onClick={() => create.mutate()} disabled={!form.name}>Create</Button></DialogFooter>
          </DialogContent>
        </Dialog>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
        {trips.map((t) => (
          <Link to={`/trips/${t.id}`} key={t.id}>
            <Card className="h-full hover:shadow-md transition-shadow">
              <CardContent className="flex flex-col gap-1 p-4">
                <div className="font-medium">{t.name}</div>
                <div className="text-sm text-muted-foreground">{t.startDate} → {t.endDate}</div>
                <span className="mt-1 inline-block rounded-full bg-secondary px-2 py-0.5 text-xs self-start">{STATUS_LABEL[t.status]}</span>
              </CardContent>
            </Card>
          </Link>
        ))}
        {trips.length === 0 && <p className="text-sm text-muted-foreground">No trips yet.</p>}
      </div>
    </div>
  )
}
