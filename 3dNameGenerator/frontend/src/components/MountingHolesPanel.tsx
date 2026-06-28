import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import type { MountingHole } from "@/types"

export interface MountingHolesPanelProps {
  holes: MountingHole[]
  onChange: (holes: MountingHole[]) => void
}

function HoleField({
  id,
  label,
  value,
  step,
  onChange,
}: {
  id: string
  label: string
  value: number
  step: number
  onChange: (value: number) => void
}) {
  return (
    <div className="flex flex-col gap-1">
      <Label htmlFor={id} className="text-xs">
        {label}
      </Label>
      <Input
        id={id}
        type="number"
        step={step}
        value={value}
        onChange={(e) => {
          const next = e.target.valueAsNumber
          if (!Number.isNaN(next)) onChange(next)
        }}
      />
    </div>
  )
}

export function MountingHolesPanel({ holes, onChange }: MountingHolesPanelProps) {
  function updateHole(index: number, patch: Partial<MountingHole>) {
    onChange(holes.map((h, i) => (i === index ? { ...h, ...patch } : h)))
  }

  function addHole() {
    onChange([...holes, { offsetXMm: 0, offsetYMm: 0, diameterMm: 4 }])
  }

  function removeHole(index: number) {
    onChange(holes.filter((_, i) => i !== index))
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-between">
        <p className="text-xs font-medium text-muted-foreground">Mounting holes</p>
        <Button variant="outline" size="sm" onClick={addHole}>
          Add hole
        </Button>
      </div>
      {holes.length === 0 && <p className="text-xs text-muted-foreground">No mounting holes.</p>}
      {holes.map((hole, index) => (
        <div key={index} className="grid grid-cols-[1fr_1fr_1fr_auto] gap-2 items-end">
          <HoleField
            id={`hole-${index}-x`}
            label="Offset X (mm)"
            value={hole.offsetXMm}
            step={0.5}
            onChange={(v) => updateHole(index, { offsetXMm: v })}
          />
          <HoleField
            id={`hole-${index}-y`}
            label="Offset Y (mm)"
            value={hole.offsetYMm}
            step={0.5}
            onChange={(v) => updateHole(index, { offsetYMm: v })}
          />
          <HoleField
            id={`hole-${index}-d`}
            label="Diameter (mm)"
            value={hole.diameterMm}
            step={0.5}
            onChange={(v) => updateHole(index, { diameterMm: v })}
          />
          <Button variant="outline" size="sm" onClick={() => removeHole(index)}>
            Remove
          </Button>
        </div>
      ))}
    </div>
  )
}
