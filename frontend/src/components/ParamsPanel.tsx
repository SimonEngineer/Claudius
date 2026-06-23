import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import type { ShapeParams, ShapeType } from "@/types"

export interface PlateParams {
  plateWidthMm: number
  plateHeightMm: number
  plateThicknessMm: number
  textDepthMm: number
}

export interface ParamsPanelProps {
  plateParams: PlateParams
  onPlateParamsChange: (params: PlateParams) => void
  shapeType: ShapeType
  shapeParams: ShapeParams
  onShapeParamsChange: (params: ShapeParams) => void
}

function NumberField({
  id,
  label,
  value,
  onChange,
  step = 1,
}: {
  id: string
  label: string
  value: number
  onChange: (value: number) => void
  step?: number
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{label}</Label>
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

export function ParamsPanel({
  plateParams,
  onPlateParamsChange,
  shapeType,
  shapeParams,
  onShapeParamsChange,
}: ParamsPanelProps) {
  return (
    <div className="flex flex-col gap-4">
      <div className="grid grid-cols-2 gap-3">
        <NumberField
          id="plate-width"
          label="Plate width (mm)"
          value={plateParams.plateWidthMm}
          onChange={(v) => onPlateParamsChange({ ...plateParams, plateWidthMm: v })}
        />
        <NumberField
          id="plate-height"
          label="Plate height (mm)"
          value={plateParams.plateHeightMm}
          onChange={(v) => onPlateParamsChange({ ...plateParams, plateHeightMm: v })}
        />
        <NumberField
          id="plate-thickness"
          label="Plate thickness (mm)"
          value={plateParams.plateThicknessMm}
          onChange={(v) => onPlateParamsChange({ ...plateParams, plateThicknessMm: v })}
          step={0.5}
        />
        <NumberField
          id="text-depth"
          label="Text depth (mm)"
          value={plateParams.textDepthMm}
          onChange={(v) => onPlateParamsChange({ ...plateParams, textDepthMm: v })}
          step={0.5}
        />
      </div>

      {(shapeType === "RoundedRectangle" || shapeType === "Plaque") && (
        <NumberField
          id="corner-radius"
          label="Corner radius (mm)"
          value={shapeParams.cornerRadiusMm}
          onChange={(v) => onShapeParamsChange({ ...shapeParams, cornerRadiusMm: v })}
          step={0.5}
        />
      )}

      {shapeType === "Star" && (
        <div className="grid grid-cols-2 gap-3">
          <NumberField
            id="star-points"
            label="Star points"
            value={shapeParams.starPoints}
            onChange={(v) => onShapeParamsChange({ ...shapeParams, starPoints: Math.round(v) })}
          />
          <NumberField
            id="star-inner-ratio"
            label="Inner radius ratio"
            value={shapeParams.starInnerRadiusRatio}
            onChange={(v) => onShapeParamsChange({ ...shapeParams, starInnerRadiusRatio: v })}
            step={0.05}
          />
        </div>
      )}
    </div>
  )
}
