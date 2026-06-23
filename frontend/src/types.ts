export type ShapeType =
  | "Rectangle"
  | "RoundedRectangle"
  | "Oval"
  | "Circle"
  | "Heart"
  | "Star"
  | "Plaque"
  | "CustomSvg"

export const SHAPE_TYPES: ShapeType[] = [
  "Rectangle",
  "RoundedRectangle",
  "Oval",
  "Circle",
  "Heart",
  "Star",
  "Plaque",
  "CustomSvg",
]

export interface ShapeParams {
  cornerRadiusMm: number
  starPoints: number
  starInnerRadiusRatio: number
  curveSegments: number
}

export const DEFAULT_SHAPE_PARAMS: ShapeParams = {
  cornerRadiusMm: 4,
  starPoints: 5,
  starInnerRadiusRatio: 0.45,
  curveSegments: 48,
}

export interface TagGenerationParams {
  text: string
  shapeType: ShapeType
  plateWidthMm: number
  plateHeightMm: number
  plateThicknessMm: number
  textDepthMm: number
  fontFamilyOrPath: string
  customSvgBytes?: string | null
  shapeParams: ShapeParams
}

export const DEFAULT_GENERATION_PARAMS: Omit<TagGenerationParams, "text"> = {
  shapeType: "RoundedRectangle",
  plateWidthMm: 70,
  plateHeightMm: 30,
  plateThicknessMm: 3,
  textDepthMm: 2,
  fontFamilyOrPath: "DejaVuSans-Bold.ttf",
  customSvgBytes: null,
  shapeParams: DEFAULT_SHAPE_PARAMS,
}

export interface TagName {
  id: number
  text: string
  sortOrder: number
}

export interface ProjectSummary {
  id: number
  name: string
  shapeType: ShapeType
  nameCount: number
  createdAt: string
}

export interface ProjectDetail {
  id: number
  name: string
  shapeType: ShapeType
  shapeParams: ShapeParams
  customSvgBase64: string | null
  fontFamilyOrPath: string
  plateWidthMm: number
  plateHeightMm: number
  plateThicknessMm: number
  textDepthMm: number
  createdAt: string
  names: TagName[]
}

export interface SaveProjectPayload {
  name: string
  shapeType: ShapeType
  shapeParams: ShapeParams
  customSvgBase64: string | null
  fontFamilyOrPath: string
  plateWidthMm: number
  plateHeightMm: number
  plateThicknessMm: number
  textDepthMm: number
}
