import { useEffect, useRef, useState } from 'react'
import { Stage, Layer, Rect, Text, Line, Image as KonvaImage } from 'react-konva'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { MediaApi } from '@/api/resources'
import { StickyNote, Pencil, Trash2, Save, ImagePlus } from 'lucide-react'

interface NoteElement {
  id: string
  type: 'note'
  x: number
  y: number
  text: string
  color: string
}
interface LineElement {
  id: string
  type: 'line'
  points: number[]
  color: string
}
interface ImageElement {
  id: string
  type: 'image'
  x: number
  y: number
  width: number
  height: number
  url: string
}
type Element = NoteElement | LineElement | ImageElement

function CanvasImage({ el, draggable, selected, onClick, onDragEnd }: {
  el: ImageElement; draggable: boolean; selected: boolean
  onClick: () => void; onDragEnd: (x: number, y: number) => void
}) {
  const [img, setImg] = useState<HTMLImageElement | null>(null)
  useEffect(() => {
    const image = new window.Image()
    image.src = el.url
    image.onload = () => setImg(image)
  }, [el.url])
  if (!img) return null
  return (
    <KonvaImage
      image={img}
      x={el.x} y={el.y} width={el.width} height={el.height}
      draggable={draggable}
      onClick={onClick}
      onDragEnd={(e) => onDragEnd(e.target.x(), e.target.y())}
      stroke={selected ? '#3b82f6' : undefined}
      strokeWidth={selected ? 2 : 0}
    />
  )
}

interface WhiteboardProps {
  contentJson: string
  onSave: (contentJson: string) => void
  saving?: boolean
}

const NOTE_COLORS = ['#fde68a', '#bfdbfe', '#bbf7d0', '#fbcfe8']

export function Whiteboard({ contentJson, onSave, saving }: WhiteboardProps) {
  const [elements, setElements] = useState<Element[]>(() => {
    try { return JSON.parse(contentJson).elements ?? [] } catch { return [] }
  })
  const [mode, setMode] = useState<'select' | 'draw'>('select')
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const drawing = useRef(false)
  const containerRef = useRef<HTMLDivElement>(null)
  const [size, setSize] = useState({ width: 800, height: 480 })

  useEffect(() => {
    setElements(() => {
      try { return JSON.parse(contentJson).elements ?? [] } catch { return [] }
    })
  }, [contentJson])

  useEffect(() => {
    const el = containerRef.current
    if (!el) return
    const update = () => setSize({ width: el.clientWidth, height: 480 })
    update()
    window.addEventListener('resize', update)
    return () => window.removeEventListener('resize', update)
  }, [])

  const addNote = () => {
    const note: NoteElement = {
      id: crypto.randomUUID(), type: 'note', x: 40, y: 40, text: 'New note',
      color: NOTE_COLORS[elements.length % NOTE_COLORS.length],
    }
    setElements((prev) => [...prev, note])
    setSelectedId(note.id)
  }

  const selected = elements.find((e) => e.id === selectedId && e.type === 'note') as NoteElement | undefined
  const [uploading, setUploading] = useState(false)

  const addImage = async (file: File) => {
    setUploading(true)
    try {
      const { url } = await MediaApi.upload(file)
      const image: ImageElement = { id: crypto.randomUUID(), type: 'image', x: 40, y: 40, width: 200, height: 150, url }
      setElements((prev) => [...prev, image])
      setSelectedId(image.id)
    } finally {
      setUploading(false)
    }
  }

  return (
    <div className="flex flex-col gap-2">
      <div className="flex items-center gap-2">
        <Button size="sm" variant="outline" onClick={addNote}><StickyNote className="h-4 w-4" /> Note</Button>
        <label>
          <input type="file" accept="image/*" className="hidden" disabled={uploading} onChange={(e) => { const f = e.target.files?.[0]; if (f) addImage(f); e.target.value = '' }} />
          <Button size="sm" variant="outline" disabled={uploading} asChild><span><ImagePlus className="h-4 w-4" /> Image</span></Button>
        </label>
        <Button size="sm" variant={mode === 'draw' ? 'default' : 'outline'} onClick={() => setMode(mode === 'draw' ? 'select' : 'draw')}>
          <Pencil className="h-4 w-4" /> Draw
        </Button>
        {selectedId && (
          <Button size="sm" variant="destructive" onClick={() => { setElements((p) => p.filter((e) => e.id !== selectedId)); setSelectedId(null) }}>
            <Trash2 className="h-4 w-4" />
          </Button>
        )}
        <Button size="sm" className="ml-auto" disabled={saving} onClick={() => onSave(JSON.stringify({ elements }))}>
          <Save className="h-4 w-4" /> Save
        </Button>
      </div>

      <div ref={containerRef} className="rounded-md border bg-white">
        <Stage
          width={size.width}
          height={size.height}
          onMouseDown={(e) => {
            if (mode !== 'draw') {
              if (e.target === e.target.getStage()) setSelectedId(null)
              return
            }
            drawing.current = true
            const pos = e.target.getStage()!.getPointerPosition()!
            setElements((prev) => [...prev, { id: crypto.randomUUID(), type: 'line', points: [pos.x, pos.y], color: '#1f2937' }])
          }}
          onMouseMove={(e) => {
            if (!drawing.current) return
            const pos = e.target.getStage()!.getPointerPosition()!
            setElements((prev) => {
              const last = prev[prev.length - 1]
              if (!last || last.type !== 'line') return prev
              const updated = { ...last, points: [...last.points, pos.x, pos.y] }
              return [...prev.slice(0, -1), updated]
            })
          }}
          onMouseUp={() => { drawing.current = false }}
        >
          <Layer>
            <Rect x={0} y={0} width={size.width} height={size.height} fill="#ffffff" listening={false} />
            {elements.map((el) => {
              if (el.type === 'line') {
                return <Line key={el.id} points={el.points} stroke={el.color} strokeWidth={3} lineCap="round" lineJoin="round" />
              }
              if (el.type === 'image') {
                return (
                  <CanvasImage
                    key={el.id}
                    el={el}
                    draggable={mode === 'select'}
                    selected={selectedId === el.id}
                    onClick={() => setSelectedId(el.id)}
                    onDragEnd={(x, y) => setElements((prev) => prev.map((p) => (p.id === el.id && p.type === 'image' ? { ...p, x, y } : p)))}
                  />
                )
              }
              return (
                <Rect
                  key={el.id}
                  x={el.x} y={el.y} width={160} height={110}
                  fill={el.color} shadowBlur={4} cornerRadius={4}
                  draggable={mode === 'select'}
                  onClick={() => setSelectedId(el.id)}
                  onDragEnd={(e) => {
                    const { x, y } = e.target.position()
                    setElements((prev) => prev.map((p) => (p.id === el.id && p.type === 'note' ? { ...p, x, y } : p)))
                  }}
                  stroke={selectedId === el.id ? '#3b82f6' : undefined}
                  strokeWidth={selectedId === el.id ? 2 : 0}
                />
              )
            })}
            {elements.map((el) =>
              el.type === 'note' ? (
                <Text
                  key={`${el.id}-text`}
                  x={el.x + 8} y={el.y + 8} width={144} height={94}
                  text={el.text} fontSize={14} fill="#1f2937"
                  listening={false}
                />
              ) : null
            )}
          </Layer>
        </Stage>
      </div>

      {selected && (
        <div className="flex flex-col gap-2 rounded-md border p-3">
          <Textarea
            value={selected.text}
            onChange={(e) => setElements((prev) => prev.map((p) => (p.id === selected.id && p.type === 'note' ? { ...p, text: e.target.value } : p)))}
          />
          <div className="flex gap-1.5">
            {NOTE_COLORS.map((c) => (
              <button
                key={c}
                className="h-6 w-6 rounded-full border"
                style={{ backgroundColor: c }}
                onClick={() => setElements((prev) => prev.map((p) => (p.id === selected.id && p.type === 'note' ? { ...p, color: c } : p)))}
              />
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
