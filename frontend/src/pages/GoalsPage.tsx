import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { GoalsApi, TripsApi } from '@/api/resources'
import { GoalKind, GoalFieldType, type GoalFieldTypeValue } from '@/types'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Label } from '@/components/ui/label'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger, DialogFooter,
} from '@/components/ui/dialog'
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select'
import { Plus, Trash2 } from 'lucide-react'

const FIELD_TYPE_LABELS: Record<GoalFieldTypeValue, string> = {
  [GoalFieldType.Text]: 'Text',
  [GoalFieldType.Number]: 'Number',
  [GoalFieldType.Date]: 'Date',
  [GoalFieldType.Url]: 'URL',
  [GoalFieldType.Boolean]: 'Yes/No',
  [GoalFieldType.Location]: 'Location',
}

interface FieldDraft { key: string; label: string; fieldType: GoalFieldTypeValue }

export function GoalsPage() {
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [icon, setIcon] = useState('')
  const [kind, setKind] = useState<number>(GoalKind.Checklist)
  const [linkedTripId, setLinkedTripId] = useState<string>('')
  const [fields, setFields] = useState<FieldDraft[]>([])
  const qc = useQueryClient()

  const { data: goals = [] } = useQuery({ queryKey: ['goals'], queryFn: GoalsApi.list })
  const { data: trips = [] } = useQuery({ queryKey: ['trips'], queryFn: TripsApi.list })

  const create = useMutation({
    mutationFn: () =>
      GoalsApi.create({
        name, description: description || undefined, icon: icon || undefined, kind,
        linkedTripId: kind === GoalKind.TripLink ? linkedTripId || null : null,
        fieldDefinitions: fields.map((f, i) => ({ ...f, sortOrder: i })),
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['goals'] })
      setOpen(false)
      setName(''); setDescription(''); setIcon(''); setKind(GoalKind.Checklist); setLinkedTripId(''); setFields([])
    },
  })

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Goals</h1>
        <Dialog open={open} onOpenChange={setOpen}>
          <DialogTrigger asChild>
            <Button><Plus className="h-4 w-4" /> New goal</Button>
          </DialogTrigger>
          <DialogContent>
            <DialogHeader><DialogTitle>Create a goal</DialogTitle></DialogHeader>
            <div className="flex flex-col gap-3">
              <div className="grid grid-cols-4 gap-2">
                <div className="col-span-1">
                  <Label>Icon</Label>
                  <Input value={icon} onChange={(e) => setIcon(e.target.value)} placeholder="🌍" />
                </div>
                <div className="col-span-3">
                  <Label>Name</Label>
                  <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="Visit all continents" />
                </div>
              </div>
              <div>
                <Label>Description</Label>
                <Textarea value={description} onChange={(e) => setDescription(e.target.value)} />
              </div>
              <div>
                <Label>Type</Label>
                <Select value={String(kind)} onValueChange={(v) => setKind(Number(v))}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value={String(GoalKind.Checklist)}>Checklist of items</SelectItem>
                    <SelectItem value={String(GoalKind.TripLink)}>Linked to a single trip</SelectItem>
                  </SelectContent>
                </Select>
              </div>

              {kind === GoalKind.TripLink ? (
                <div>
                  <Label>Trip</Label>
                  <Select value={linkedTripId} onValueChange={setLinkedTripId}>
                    <SelectTrigger><SelectValue placeholder="Select a trip" /></SelectTrigger>
                    <SelectContent>
                      {trips.map((t) => <SelectItem key={t.id} value={t.id}>{t.name}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
              ) : (
                <div>
                  <Label>Custom fields per item (optional)</Label>
                  <p className="text-xs text-muted-foreground mb-2">e.g. for "MJ statues": Sculptor (Text), Year (Number)</p>
                  <div className="flex flex-col gap-2">
                    {fields.map((f, i) => (
                      <div key={i} className="flex gap-2">
                        <Input
                          placeholder="Field label"
                          value={f.label}
                          onChange={(e) => setFields((prev) => prev.map((p, idx) => idx === i ? { ...p, label: e.target.value, key: e.target.value.toLowerCase().replace(/\s+/g, '_') } : p))}
                        />
                        <Select value={String(f.fieldType)} onValueChange={(v) => setFields((prev) => prev.map((p, idx) => idx === i ? { ...p, fieldType: Number(v) as GoalFieldTypeValue } : p))}>
                          <SelectTrigger className="w-32"><SelectValue /></SelectTrigger>
                          <SelectContent>
                            {Object.entries(FIELD_TYPE_LABELS).map(([v, label]) => <SelectItem key={v} value={v}>{label}</SelectItem>)}
                          </SelectContent>
                        </Select>
                        <Button variant="ghost" size="icon" onClick={() => setFields((prev) => prev.filter((_, idx) => idx !== i))}>
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </div>
                    ))}
                    <Button variant="outline" size="sm" className="self-start" onClick={() => setFields((prev) => [...prev, { key: '', label: '', fieldType: GoalFieldType.Text }])}>
                      <Plus className="h-3 w-3" /> Add field
                    </Button>
                  </div>
                </div>
              )}
            </div>
            <DialogFooter>
              <Button onClick={() => create.mutate()} disabled={!name || create.isPending}>Create goal</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
        {goals.map((g) => (
          <Link to={`/goals/${g.id}`} key={g.id}>
            <Card className="h-full hover:shadow-md transition-shadow">
              <CardContent className="flex flex-col gap-2 p-4">
                <div className="text-lg font-medium">{g.icon} {g.name}</div>
                <p className="text-sm text-muted-foreground line-clamp-2">{g.description}</p>
                {g.kind === GoalKind.Checklist && (
                  <div className="mt-1">
                    <div className="h-2 w-full overflow-hidden rounded-full bg-muted">
                      <div className="h-full bg-primary" style={{ width: `${g.itemCount ? (g.completedCount / g.itemCount) * 100 : 0}%` }} />
                    </div>
                    <p className="mt-1 text-xs text-muted-foreground">{g.completedCount}/{g.itemCount} completed</p>
                  </div>
                )}
              </CardContent>
            </Card>
          </Link>
        ))}
        {goals.length === 0 && <p className="text-sm text-muted-foreground">No goals yet — create one to get started.</p>}
      </div>
    </div>
  )
}
