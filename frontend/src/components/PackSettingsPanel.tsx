import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import type { PackSettings } from "@/types"

export interface PackSettingsPanelProps {
  value: PackSettings
  onChange: (value: PackSettings) => void
}

function Field({
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

/** Lets a project tune the wine-glass charm's hook and the clothes clip's spring so a pack
 * actually fits a particular glass stem or fabric thickness, instead of one fixed size for everyone. */
export function PackSettingsPanel({ value, onChange }: PackSettingsPanelProps) {
  function set(patch: Partial<PackSettings>) {
    onChange({ ...value, ...patch })
  }

  return (
    <div className="flex flex-col gap-3">
      <p className="text-xs font-medium text-muted-foreground">Pack: wine-glass charm</p>
      <div className="grid grid-cols-2 gap-2">
        <Field
          id="charm-plate-width"
          label="Plate width (mm)"
          value={value.charmPlateWidthMm}
          step={1}
          onChange={(v) => set({ charmPlateWidthMm: v })}
        />
        <Field
          id="charm-plate-height"
          label="Plate height (mm)"
          value={value.charmPlateHeightMm}
          step={1}
          onChange={(v) => set({ charmPlateHeightMm: v })}
        />
        <Field
          id="charm-hook-radius"
          label="Hook outer radius (mm)"
          value={value.charmHookOuterRadiusMm}
          step={0.5}
          onChange={(v) => set({ charmHookOuterRadiusMm: v })}
        />
        <Field
          id="charm-hook-band"
          label="Hook band thickness (mm)"
          value={value.charmHookBandThicknessMm}
          step={0.1}
          onChange={(v) => set({ charmHookBandThicknessMm: v })}
        />
      </div>

      <p className="text-xs font-medium text-muted-foreground">Pack: clothes clip</p>
      <div className="grid grid-cols-2 gap-2">
        <Field
          id="clip-plate-width"
          label="Plate width (mm)"
          value={value.clipPlateWidthMm}
          step={1}
          onChange={(v) => set({ clipPlateWidthMm: v })}
        />
        <Field
          id="clip-plate-height"
          label="Plate height (mm)"
          value={value.clipPlateHeightMm}
          step={1}
          onChange={(v) => set({ clipPlateHeightMm: v })}
        />
        <Field
          id="clip-arm-length"
          label="Arm length (mm)"
          value={value.clipArmLengthMm}
          step={1}
          onChange={(v) => set({ clipArmLengthMm: v })}
        />
        <Field
          id="clip-gap"
          label="Gap / fabric thickness (mm)"
          value={value.clipGapMm}
          step={0.1}
          onChange={(v) => set({ clipGapMm: v })}
        />
        <Field
          id="clip-arm-thickness"
          label="Arm thickness (mm)"
          value={value.clipArmThicknessMm}
          step={0.1}
          onChange={(v) => set({ clipArmThicknessMm: v })}
        />
      </div>
    </div>
  )
}
