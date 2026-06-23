import { useRef } from "react"
import { Label } from "@/components/ui/label"
import { Button } from "@/components/ui/button"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { SHAPE_TYPES, type ShapeType } from "@/types"
import { uploadSvg } from "@/api/shapesApi"

export interface ShapeSelectorProps {
  shapeType: ShapeType
  onShapeTypeChange: (shapeType: ShapeType) => void
  customSvgBytes: string | null
  onCustomSvgBytesChange: (bytes: string | null) => void
}

export function ShapeSelector({
  shapeType,
  onShapeTypeChange,
  customSvgBytes,
  onCustomSvgBytesChange,
}: ShapeSelectorProps) {
  const fileInputRef = useRef<HTMLInputElement>(null)

  async function handleFileSelected(file: File) {
    try {
      const bytes = await uploadSvg(file)
      onCustomSvgBytesChange(bytes)
    } catch (err) {
      onCustomSvgBytesChange(null)
      alert(err instanceof Error ? err.message : "SVG upload failed")
    }
  }

  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor="shape-select">Shape</Label>
      <Select
        value={shapeType}
        onValueChange={(v) => onShapeTypeChange(v as ShapeType)}
      >
        <SelectTrigger id="shape-select">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {SHAPE_TYPES.map((shape) => (
            <SelectItem key={shape} value={shape}>
              {shape}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      {shapeType === "CustomSvg" && (
        <div className="flex items-center gap-2 pt-1">
          <input
            ref={fileInputRef}
            type="file"
            accept=".svg"
            className="hidden"
            onChange={(e) => {
              const file = e.target.files?.[0]
              if (file) void handleFileSelected(file)
            }}
          />
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => fileInputRef.current?.click()}
          >
            Upload SVG outline
          </Button>
          <span className="text-sm text-muted-foreground">
            {customSvgBytes ? "SVG loaded" : "No file selected"}
          </span>
        </div>
      )}
    </div>
  )
}
