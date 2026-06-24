import { useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { GoalsApi } from '@/api/resources'
import { EntityType, GoalKind, GoalFieldType, type GoalFieldTypeValue } from '@/types'
import type { GoalItem } from '@/types'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Checkbox } from '@/components/ui/checkbox'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger, DialogFooter,
} from '@/components/ui/dialog'
import { TagPicker } from '@/components/TagPicker'
import { MediaGallery } from '@/components/MediaGallery'
import { LocationMap } from '@/components/LocationMap'
import { useToast, getErrorMessage } from '@/components/ui/toast'
import { ArrowLeft, Plus, Trash2, Settings, MapPin, Pencil } from 'lucide-react'

const FIELD_TYPE_LABELS: Record<GoalFieldTypeValue, string> = {
  [GoalFieldType.Text]: 'Text',
  [GoalFieldType.Number]: 'Number',
  [GoalFieldType.Date]: 'Date',
  [GoalFieldType.Url]: 'URL',
  [GoalFieldType.Boolean]: 'Yes/No',
  [GoalFieldType.Location]: 'Location',
}

export function GoalDetailPage() {
  const { id = '' } = useParams()
  const qc = useQueryClient()
  const [addOpen, setAddOpen] = useState(false)
  const [fieldsOpen, setFieldsOpen] = useState(false)
  const [newField, setNewField] = useState({ label: '', fieldType: GoalFieldType.Text as GoalFieldTypeValue })
  const [itemDraft, setItemDraft] = useState<Record<string, string>>({})
  const [activeItemId, setActiveItemId] = useState<string | null>(null)
  const [editingItemId, setEditingItemId] = useState<string | null>(null)
  const { showError } = useToast()
  const onErr = (err: unknown) => showError(getErrorMessage(err))

  const { data: goal } = useQuery({ queryKey: ['goal', id], queryFn: () => GoalsApi.get(id) })
  const { data: items = [] } = useQuery({ queryKey: ['goal', id, 'items'], queryFn: () => GoalsApi.items(id) })

  const closeItemDialog = () => { setAddOpen(false); setItemDraft({}); setEditingItemId(null) }

  const startEditItem = (item: GoalItem) => {
    setEditingItemId(item.id)
    setItemDraft({
      name: item.name,
      lat: item.lat?.toString() ?? '',
      lng: item.lng?.toString() ?? '',
      ...Object.fromEntries(item.fieldValues.map((v) => [v.goalFieldDefinitionId, v.value ?? ''])),
    })
    setAddOpen(true)
  }

  const saveItem = useMutation({
    mutationFn: () => {
      const { name, lat, lng, ...rest } = itemDraft
      const payload = {
        name: name ?? '',
        lat: lat ? parseFloat(lat) : null,
        lng: lng ? parseFloat(lng) : null,
        fieldValues: Object.entries(rest).map(([fid, value]) => ({ goalFieldDefinitionId: fid, value })),
      }
      return editingItemId ? GoalsApi.updateItem(editingItemId, payload) : GoalsApi.addItem(id, payload)
    },
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['goal', id, 'items'] }); closeItemDialog() },
    onError: onErr,
  })

  const removeItem = useMutation({
    mutationFn: (itemId: string) => GoalsApi.removeItem(itemId),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['goal', id, 'items'] }); setActiveItemId(null) },
    onError: onErr,
  })

  const toggleComplete = useMutation({
    mutationFn: ({ itemId, completed }: { itemId: string; completed: boolean }) => GoalsApi.completeItem(itemId, completed),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['goal', id, 'items'] }),
    onError: onErr,
  })

  const addField = useMutation({
    mutationFn: () => GoalsApi.addField(id, {
      key: newField.label.toLowerCase().replace(/\s+/g, '_'), label: newField.label,
      fieldType: newField.fieldType, sortOrder: goal?.fieldDefinitions.length ?? 0,
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['goal', id] }); setNewField({ label: '', fieldType: GoalFieldType.Text }) },
    onError: onErr,
  })

  const removeField = useMutation({
    mutationFn: (fieldId: string) => GoalsApi.removeField(fieldId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['goal', id] }),
    onError: onErr,
  })

  if (!goal) return null

  const pins = items.filter((i) => i.lat != null && i.lng != null).map((i) => ({ id: i.id, lat: i.lat!, lng: i.lng!, label: i.name }))
  const activeItem = items.find((i) => i.id === activeItemId);

  return (
    <div className="flex flex-col gap-4">
      <Link to="/goals" className="flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="h-4 w-4" /> Back to goals
      </Link>

      <div>
        <h1 className="text-2xl font-bold">{goal.icon} {goal.name}</h1>
        <p className="text-muted-foreground">{goal.description}</p>
      </div>

      <TagPicker entityType={EntityType.Goal} entityId={id} selected={goal.tags} onChange={() => qc.invalidateQueries({ queryKey: ['goal', id] })} />

      {goal.kind === GoalKind.TripLink ? (
        <Card><CardContent className="p-4">
          {goal.linkedTripId ? (
            <Link to={`/trips/${goal.linkedTripId}`} className="text-primary underline">Open linked trip →</Link>
          ) : (
            <p className="text-sm text-muted-foreground">No trip linked yet.</p>
          )}
        </CardContent></Card>
      ) : (
        <>
          {pins.length > 0 && <LocationMap pins={pins} height={300} onPinClick={setActiveItemId} />}

          <div className="flex items-center justify-between">
            <h2 className="text-lg font-semibold">Items</h2>
            <div className="flex gap-2">
              <Dialog open={fieldsOpen} onOpenChange={setFieldsOpen}>
                <DialogTrigger asChild><Button size="sm" variant="outline"><Settings className="h-4 w-4" /> Manage fields</Button></DialogTrigger>
                <DialogContent>
                  <DialogHeader><DialogTitle>Custom fields</DialogTitle></DialogHeader>
                  <div className="flex flex-col gap-2">
                    {goal.fieldDefinitions.map((f) => (
                      <div key={f.id} className="flex items-center justify-between rounded-md border p-2">
                        <span className="text-sm">{f.label} <span className="text-muted-foreground">({FIELD_TYPE_LABELS[f.fieldType]})</span></span>
                        <button onClick={() => removeField.mutate(f.id)}><Trash2 className="h-4 w-4 text-muted-foreground" /></button>
                      </div>
                    ))}
                    {goal.fieldDefinitions.length === 0 && <p className="text-sm text-muted-foreground">No custom fields yet.</p>}
                    <div className="flex gap-2 mt-2">
                      <Input placeholder="Field label" value={newField.label} onChange={(e) => setNewField({ ...newField, label: e.target.value })} />
                      <Select value={String(newField.fieldType)} onValueChange={(v) => setNewField({ ...newField, fieldType: Number(v) as GoalFieldTypeValue })}>
                        <SelectTrigger className="w-32"><SelectValue /></SelectTrigger>
                        <SelectContent>
                          {Object.entries(FIELD_TYPE_LABELS).map(([v, label]) => <SelectItem key={v} value={v}>{label}</SelectItem>)}
                        </SelectContent>
                      </Select>
                      <Button onClick={() => addField.mutate()} disabled={!newField.label}><Plus className="h-4 w-4" /></Button>
                    </div>
                  </div>
                </DialogContent>
              </Dialog>
              <Dialog open={addOpen} onOpenChange={(o) => (o ? setAddOpen(true) : closeItemDialog())}>
                <DialogTrigger asChild><Button size="sm" onClick={() => { setEditingItemId(null); setItemDraft({}) }}><Plus className="h-4 w-4" /> Add item</Button></DialogTrigger>
                <DialogContent className="max-h-[85vh] overflow-y-auto">
                  <DialogHeader><DialogTitle>{editingItemId ? 'Edit item' : 'Add item'}</DialogTitle></DialogHeader>
                  <div className="flex flex-col gap-3">
                    <div>
                      <Label>Name</Label>
                      <Input value={itemDraft.name ?? ''} onChange={(e) => setItemDraft({ ...itemDraft, name: e.target.value })} />
                    </div>
                    <div>
                      <Label className="flex items-center gap-1"><MapPin className="h-3 w-3" /> Click the map to set location (optional)</Label>
                      <LocationMap
                        height={200}
                        pins={itemDraft.lat && itemDraft.lng ? [{ id: 'pick', lat: parseFloat(itemDraft.lat), lng: parseFloat(itemDraft.lng), label: itemDraft.name || 'New item' }] : []}
                        center={itemDraft.lat && itemDraft.lng ? [parseFloat(itemDraft.lat), parseFloat(itemDraft.lng)] : undefined}
                        zoom={itemDraft.lat && itemDraft.lng ? 8 : 2}
                        onPick={(lat, lng) => setItemDraft({ ...itemDraft, lat: lat.toFixed(5), lng: lng.toFixed(5) })}
                      />
                    </div>
                    <div className="grid grid-cols-2 gap-2">
                      <div><Label className="text-xs">Lat</Label><Input value={itemDraft.lat ?? ''} onChange={(e) => setItemDraft({ ...itemDraft, lat: e.target.value })} /></div>
                      <div><Label className="text-xs">Lng</Label><Input value={itemDraft.lng ?? ''} onChange={(e) => setItemDraft({ ...itemDraft, lng: e.target.value })} /></div>
                    </div>
                    {goal.fieldDefinitions.map((f) => (
                      <div key={f.id}>
                        <Label>{f.label}</Label>
                        <Input
                          type={f.fieldType === GoalFieldType.Date ? 'date' : f.fieldType === GoalFieldType.Number ? 'number' : 'text'}
                          value={itemDraft[f.id] ?? ''}
                          onChange={(e) => setItemDraft({ ...itemDraft, [f.id]: e.target.value })}
                        />
                      </div>
                    ))}
                  </div>
                  <DialogFooter>
                    <Button onClick={() => saveItem.mutate()} disabled={!itemDraft.name}>{editingItemId ? 'Save' : 'Add'}</Button>
                  </DialogFooter>
                </DialogContent>
              </Dialog>
            </div>
          </div>

          <div className="overflow-x-auto rounded-md border">
            <table className="w-full text-sm">
              <thead className="bg-muted text-left">
                <tr>
                  <th className="p-2 w-10"></th>
                  <th className="p-2">Name</th>
                  {goal.fieldDefinitions.map((f) => <th className="p-2" key={f.id}>{f.label}</th>)}
                  <th className="p-2 w-16"></th>
                </tr>
              </thead>
              <tbody>
                {items.map((item) => (
                  <tr key={item.id} className="border-t cursor-pointer hover:bg-accent" onClick={() => setActiveItemId(item.id)}>
                    <td className="p-2" onClick={(e) => e.stopPropagation()}>
                      <Checkbox checked={item.isCompleted} onCheckedChange={(c) => toggleComplete.mutate({ itemId: item.id, completed: !!c })} />
                    </td>
                    <td className={`p-2 ${item.isCompleted ? 'line-through text-muted-foreground' : ''}`}>{item.name}</td>
                    {goal.fieldDefinitions.map((f) => (
                      <td className="p-2" key={f.id}>{item.fieldValues.find((v) => v.goalFieldDefinitionId === f.id)?.value}</td>
                    ))}
                    <td className="p-2" onClick={(e) => e.stopPropagation()}>
                      <div className="flex gap-1">
                        <button onClick={() => startEditItem(item)}><Pencil className="h-3.5 w-3.5 text-muted-foreground" /></button>
                        <button onClick={() => { if (confirm(`Delete "${item.name}"?`)) removeItem.mutate(item.id) }}><Trash2 className="h-3.5 w-3.5 text-muted-foreground" /></button>
                      </div>
                    </td>
                  </tr>
                ))}
                {items.length === 0 && <tr><td colSpan={3 + goal.fieldDefinitions.length} className="p-4 text-center text-muted-foreground">No items yet.</td></tr>}
              </tbody>
            </table>
          </div>
        </>
      )}

      {activeItem && (
        <Dialog open onOpenChange={(o) => !o && setActiveItemId(null)}>
          <DialogContent>
            <ItemDrawer
              goalId={id}
              item={activeItem}
              fieldDefinitions={goal.fieldDefinitions}
              onToggle={(completed) => toggleComplete.mutate({ itemId: activeItem.id, completed })}
              onEdit={() => { setActiveItemId(null); startEditItem(activeItem) }}
              onDelete={() => removeItem.mutate(activeItem.id)}
            />
          </DialogContent>
        </Dialog>
      )}
    </div>
  )
}

function ItemDrawer({
  goalId, item, fieldDefinitions, onToggle, onEdit, onDelete,
}: { goalId: string; item: GoalItem; fieldDefinitions: { id: string; label: string }[]; onToggle: (completed: boolean) => void; onEdit: () => void; onDelete: () => void }) {
  const qc = useQueryClient()
  const [noteText, setNoteText] = useState('')
  const [linkUrl, setLinkUrl] = useState('')
  const { showError } = useToast()
  const onErr = (err: unknown) => showError(getErrorMessage(err))

  const addNote = useMutation({
    mutationFn: () => GoalsApi.addItemNote(item.id, noteText),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['goal', goalId, 'items'] }); setNoteText('') },
    onError: onErr,
  })
  const addLink = useMutation({
    mutationFn: () => GoalsApi.addItemLink(item.id, linkUrl),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['goal', goalId, 'items'] }); setLinkUrl('') },
    onError: onErr,
  })

  return (
    <div className="flex flex-col gap-4">
      <DialogHeader>
        <DialogTitle className="flex items-center justify-between gap-2">
          <span className="flex items-center gap-2">
            <Checkbox checked={item.isCompleted} onCheckedChange={(c) => onToggle(!!c)} />
            {item.name}
          </span>
          <span className="flex items-center gap-1">
            <button onClick={onEdit}><Pencil className="h-4 w-4 text-muted-foreground" /></button>
            <button onClick={() => { if (confirm(`Delete "${item.name}"?`)) onDelete() }}><Trash2 className="h-4 w-4 text-muted-foreground" /></button>
          </span>
        </DialogTitle>
      </DialogHeader>

      <div className="grid grid-cols-2 gap-2 text-sm">
        {fieldDefinitions.map((f) => (
          <div key={f.id}>
            <span className="text-muted-foreground">{f.label}: </span>
            {item.fieldValues.find((v) => v.goalFieldDefinitionId === f.id)?.value}
          </div>
        ))}
      </div>

      <TagPicker entityType={EntityType.GoalItem} entityId={item.id} selected={item.tags} onChange={() => qc.invalidateQueries({ queryKey: ['goal', goalId, 'items'] })} />

      <div>
        <Label>Photos / memorabilia</Label>
        <MediaGallery entityType={EntityType.GoalItem} entityId={item.id} />
      </div>

      <div>
        <Label>Notes</Label>
        <div className="flex flex-col gap-2">
          {item.notes.map((n) => <p key={n.id} className="rounded-md border p-2 text-sm">{n.text}</p>)}
          <div className="flex gap-2">
            <Textarea value={noteText} onChange={(e) => setNoteText(e.target.value)} placeholder="Add a note..." />
            <Button onClick={() => addNote.mutate()} disabled={!noteText}>Add</Button>
          </div>
        </div>
      </div>

      <div>
        <Label>Links</Label>
        <div className="flex flex-col gap-2">
          {item.links.map((l) => <a key={l.id} href={l.url} target="_blank" rel="noreferrer" className="text-sm text-primary underline">{l.url}</a>)}
          <div className="flex gap-2">
            <Input value={linkUrl} onChange={(e) => setLinkUrl(e.target.value)} placeholder="https://..." />
            <Button onClick={() => addLink.mutate()} disabled={!linkUrl}>Add</Button>
          </div>
        </div>
      </div>
    </div>
  )
}
