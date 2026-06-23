import { useMemo, useState } from "react"
import { NameListInput, namesFromText } from "@/components/NameListInput"
import { ShapeSelector } from "@/components/ShapeSelector"
import { ParamsPanel, type PlateParams } from "@/components/ParamsPanel"
import { NameTagPreview } from "@/components/NameTagPreview"
import { DEFAULT_GENERATION_PARAMS, type ShapeParams, type ShapeType } from "@/types"

function App() {
  const [namesText, setNamesText] = useState("Alice\nBob")
  const [shapeType, setShapeType] = useState<ShapeType>(DEFAULT_GENERATION_PARAMS.shapeType)
  const [customSvgBytes, setCustomSvgBytes] = useState<string | null>(null)
  const [plateParams, setPlateParams] = useState<PlateParams>({
    plateWidthMm: DEFAULT_GENERATION_PARAMS.plateWidthMm,
    plateHeightMm: DEFAULT_GENERATION_PARAMS.plateHeightMm,
    plateThicknessMm: DEFAULT_GENERATION_PARAMS.plateThicknessMm,
    textDepthMm: DEFAULT_GENERATION_PARAMS.textDepthMm,
  })
  const [shapeParams, setShapeParams] = useState<ShapeParams>(
    DEFAULT_GENERATION_PARAMS.shapeParams
  )

  const names = useMemo(() => namesFromText(namesText), [namesText])

  return (
    <div className="min-h-svh bg-background p-6">
      <h1 className="text-xl font-semibold mb-4">Name Tags</h1>

      <div className="grid grid-cols-1 lg:grid-cols-[320px_1fr] gap-6">
        <div className="flex flex-col gap-4">
          <NameListInput value={namesText} onChange={setNamesText} />
          <ShapeSelector
            shapeType={shapeType}
            onShapeTypeChange={setShapeType}
            customSvgBytes={customSvgBytes}
            onCustomSvgBytesChange={setCustomSvgBytes}
          />
          <ParamsPanel
            plateParams={plateParams}
            onPlateParamsChange={setPlateParams}
            shapeType={shapeType}
            shapeParams={shapeParams}
            onShapeParamsChange={setShapeParams}
          />
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-4">
          {names.map((name) => (
            <NameTagPreview
              key={name}
              params={{
                text: name,
                shapeType,
                customSvgBytes,
                shapeParams,
                fontFamilyOrPath: DEFAULT_GENERATION_PARAMS.fontFamilyOrPath,
                ...plateParams,
              }}
            />
          ))}
          {names.length === 0 && (
            <p className="text-sm text-muted-foreground">
              Enter at least one name to see a preview.
            </p>
          )}
        </div>
      </div>
    </div>
  )
}

export default App
