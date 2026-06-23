import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { TagsApi } from '@/api/resources'
import type { Tag, EntityTypeValue } from '@/types'
import { X, Plus } from 'lucide-react'

interface TagPickerProps {
  entityType: EntityTypeValue
  entityId: string
  selected: Tag[]
  onChange?: () => void
}

export function TagPicker({ entityType, entityId, selected, onChange }: TagPickerProps) {
  const [input, setInput] = useState('')
  const qc = useQueryClient()
  const { data: allTags = [] } = useQuery({ queryKey: ['tags'], queryFn: TagsApi.list })

  const assign = useMutation({
    mutationFn: async (name: string) => {
      const tag = await TagsApi.create(name)
      await TagsApi.assign(tag.id, entityType, entityId)
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['tags'] })
      onChange?.()
      setInput('')
    },
  })

  const unassign = useMutation({
    mutationFn: (tagId: string) => TagsApi.unassign(tagId, entityType, entityId),
    onSuccess: () => onChange?.(),
  })

  const suggestions = allTags.filter(
    (t) => !selected.some((s) => s.id === t.id) && t.name.toLowerCase().includes(input.toLowerCase()) && input.length > 0
  )

  return (
    <div className="flex flex-col gap-2">
      <div className="flex flex-wrap gap-1.5">
        {selected.map((tag) => (
          <Badge key={tag.id} style={{ backgroundColor: tag.color, color: 'white', borderColor: tag.color }}>
            {tag.name}
            <button onClick={() => unassign.mutate(tag.id)} className="ml-1">
              <X className="h-3 w-3" />
            </button>
          </Badge>
        ))}
      </div>
      <div className="relative flex gap-1.5">
        <Input
          value={input}
          onChange={(e) => setInput(e.target.value)}
          placeholder="Add tag..."
          className="h-7 text-xs max-w-40"
          onKeyDown={(e) => {
            if (e.key === 'Enter' && input.trim()) assign.mutate(input.trim())
          }}
        />
        <Button size="icon" className="h-7 w-7" variant="outline" onClick={() => input.trim() && assign.mutate(input.trim())}>
          <Plus className="h-3 w-3" />
        </Button>
        {suggestions.length > 0 && (
          <div className="absolute top-8 left-0 z-10 w-40 rounded-md border bg-card shadow-md">
            {suggestions.slice(0, 5).map((s) => (
              <button
                key={s.id}
                className="block w-full px-2 py-1 text-left text-xs hover:bg-accent"
                onClick={() => assign.mutate(s.name)}
              >
                {s.name}
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
