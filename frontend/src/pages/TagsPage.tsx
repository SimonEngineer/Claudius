import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { TagsApi } from '@/api/resources'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { useToast, getErrorMessage } from '@/components/ui/toast'
import { Trash2, Check } from 'lucide-react'

export function TagsPage() {
  const qc = useQueryClient()
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editForm, setEditForm] = useState({ name: '', color: '#64748b' })
  const { showError } = useToast()
  const onErr = (err: unknown) => showError(getErrorMessage(err))

  const { data: tags = [] } = useQuery({ queryKey: ['tags', 'usage'], queryFn: TagsApi.usage })

  const update = useMutation({
    mutationFn: () => TagsApi.update(editingId!, editForm.name, editForm.color),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['tags'] }); setEditingId(null) },
    onError: onErr,
  })

  const remove = useMutation({
    mutationFn: (id: string) => TagsApi.remove(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['tags'] }),
    onError: onErr,
  })

  const startEdit = (id: string, name: string, color: string) => {
    setEditingId(id)
    setEditForm({ name, color })
  }

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-2xl font-bold">Tags</h1>
      <p className="text-sm text-muted-foreground">Rename, recolor, or delete tags used across your wishlist, goals, and trips.</p>

      <Card>
        <CardContent className="flex flex-col gap-2 p-4">
          {tags.map((t) => (
            <div key={t.id} className="flex items-center gap-3 rounded-md border p-2">
              {editingId === t.id ? (
                <>
                  <input type="color" value={editForm.color} onChange={(e) => setEditForm({ ...editForm, color: e.target.value })} className="h-8 w-8 rounded border" />
                  <Input className="max-w-48" value={editForm.name} onChange={(e) => setEditForm({ ...editForm, name: e.target.value })} />
                  <Button size="icon" variant="outline" onClick={() => update.mutate()} disabled={!editForm.name || update.isPending}>
                    <Check className="h-4 w-4" />
                  </Button>
                </>
              ) : (
                <>
                  <Badge style={{ backgroundColor: t.color, color: 'white' }} className="cursor-pointer" onClick={() => startEdit(t.id, t.name, t.color)}>
                    {t.name}
                  </Badge>
                  <span className="text-sm text-muted-foreground">{t.usageCount} use{t.usageCount === 1 ? '' : 's'}</span>
                  <div className="flex-1" />
                  <button
                    onClick={() => { if (confirm(`Delete tag "${t.name}"? It will be removed from everything it's applied to.`)) remove.mutate(t.id) }}
                  >
                    <Trash2 className="h-4 w-4 text-muted-foreground" />
                  </button>
                </>
              )}
            </div>
          ))}
          {tags.length === 0 && <p className="text-sm text-muted-foreground">No tags yet.</p>}
        </CardContent>
      </Card>
    </div>
  )
}
