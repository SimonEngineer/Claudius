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

export type TextHorizontalAlign = "Left" | "Center" | "Right"
export type TextVerticalAlign = "Top" | "Center" | "Bottom"

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

export interface MountingHole {
  id?: number
  offsetXMm: number
  offsetYMm: number
  diameterMm: number
}

export interface TagGenerationParams {
  text: string
  shapeType: ShapeType
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
  bevelMm: number
  fontFamilyOrPath: string
  customSvgBytes?: string | null
  shapeParams: ShapeParams
  mountingHoles: MountingHole[]
}

export const DEFAULT_GENERATION_PARAMS: Omit<TagGenerationParams, "text"> = {
  shapeType: "RoundedRectangle",
  plateWidthMm: 70,
  plateHeightMm: 30,
  plateThicknessMm: 3,
  textDepthMm: 2,
  textMarginLeftMm: 5,
  textMarginRightMm: 5,
  textMarginTopMm: 5,
  textMarginBottomMm: 5,
  textHorizontalAlign: "Center",
  textVerticalAlign: "Center",
  bevelMm: 0,
  fontFamilyOrPath: "DejaVuSans-Bold.ttf",
  customSvgBytes: null,
  shapeParams: DEFAULT_SHAPE_PARAMS,
  mountingHoles: [],
}

export interface PackSettings {
  charmPlateWidthMm: number
  charmPlateHeightMm: number
  charmHookOuterRadiusMm: number
  charmHookBandThicknessMm: number
  clipPlateWidthMm: number
  clipPlateHeightMm: number
  clipArmLengthMm: number
  clipGapMm: number
  clipArmThicknessMm: number
}

export const DEFAULT_PACK_SETTINGS: PackSettings = {
  charmPlateWidthMm: 28,
  charmPlateHeightMm: 13,
  charmHookOuterRadiusMm: 6,
  charmHookBandThicknessMm: 1.8,
  clipPlateWidthMm: 55,
  clipPlateHeightMm: 22,
  clipArmLengthMm: 20,
  clipGapMm: 2.4,
  clipArmThicknessMm: 1.2,
}

export interface TagName {
  id: number
  text: string
  sortOrder: number
  textDepthMmOverride: number | null
}

export const MAX_NAME_TEXT_LENGTH = 40

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
  textMarginLeftMm: number
  textMarginRightMm: number
  textMarginTopMm: number
  textMarginBottomMm: number
  textHorizontalAlign: TextHorizontalAlign
  textVerticalAlign: TextVerticalAlign
  bevelMm: number
  createdAt: string
  names: TagName[]
  mountingHoles: MountingHole[]
}
export interface ProjectDetail extends PackSettings {}

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
  textMarginLeftMm: number
  textMarginRightMm: number
  textMarginTopMm: number
  textMarginBottomMm: number
  textHorizontalAlign: TextHorizontalAlign
  textVerticalAlign: TextVerticalAlign
  bevelMm: number
  mountingHoles: MountingHole[]
}
export interface SaveProjectPayload extends PackSettings {}
