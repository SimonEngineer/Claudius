import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import type { ShapeParams, ShapeType, TextHorizontalAlign, TextVerticalAlign } from "@/types"

export interface PlateParams {
  plateWidthMm: number
  plateHeightMm: number
  plateThicknessMm: number
  textDepthMm: number
  textMarginLeftMm: number
  textMarginRightMm: number
  textMarginTopMm: number
  textMarginBottomMm: number
  textHorizontalAlign: TextHorizontalAlign
  textVerticalAlign: TextVerticalAlign
}

const HORIZONTAL_ALIGNS: TextHorizontalAlign[] = ["Left", "Center", "Right"]
const VERTICAL_ALIGNS: TextVerticalAlign[] = ["Top", "Center", "Bottom"]

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

function SelectField<T extends string>({
  id,
  label,
  value,
  options,
  onChange,
}: {
  id: string
  label: string
  value: T
  options: T[]
  onChange: (value: T) => void
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      <select
        id={id}
        className="flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
        value={value}
        onChange={(e) => onChange(e.target.value as T)}
      >
        {options.map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </select>
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

      <div className="flex flex-col gap-3">
        <p className="text-xs font-medium text-muted-foreground">Text margins (mm)</p>
        <div className="grid grid-cols-2 gap-3">
          <NumberField
            id="margin-left"
            label="Left"
            value={plateParams.textMarginLeftMm}
            onChange={(v) => onPlateParamsChange({ ...plateParams, textMarginLeftMm: v })}
            step={0.5}
          />
          <NumberField
            id="margin-right"
            label="Right"
            value={plateParams.textMarginRightMm}
            onChange={(v) => onPlateParamsChange({ ...plateParams, textMarginRightMm: v })}
            step={0.5}
          />
          <NumberField
            id="margin-top"
            label="Top"
            value={plateParams.textMarginTopMm}
            onChange={(v) => onPlateParamsChange({ ...plateParams, textMarginTopMm: v })}
            step={0.5}
          />
          <NumberField
            id="margin-bottom"
            label="Bottom"
            value={plateParams.textMarginBottomMm}
            onChange={(v) => onPlateParamsChange({ ...plateParams, textMarginBottomMm: v })}
            step={0.5}
          />
        </div>
        <div className="grid grid-cols-2 gap-3">
          <SelectField
            id="text-h-align"
            label="Horizontal align"
            value={plateParams.textHorizontalAlign}
            options={HORIZONTAL_ALIGNS}
            onChange={(v) => onPlateParamsChange({ ...plateParams, textHorizontalAlign: v })}
          />
          <SelectField
            id="text-v-align"
            label="Vertical align"
            value={plateParams.textVerticalAlign}
            options={VERTICAL_ALIGNS}
            onChange={(v) => onPlateParamsChange({ ...plateParams, textVerticalAlign: v })}
          />
        </div>
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
