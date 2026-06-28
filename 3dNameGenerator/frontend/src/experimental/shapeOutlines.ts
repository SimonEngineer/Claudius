import * as THREE from "three"

/**
 * JS ports of the parametric plate-outline formulas implemented in C# under
 * backend/NameTags.Core/Outlines/*OutlineProvider.cs. Kept deliberately minimal --
 * only the closed-form presets (Rectangle, RoundedRectangle, Circle, Oval) needed
 * for this experimental page. Heart/Star/Plaque/CustomSvg are out of scope here;
 * see the M10 plan for rationale. Comment pointing back at the C# source so the
 * two implementations don't silently drift.
 */
export type ExperimentalShapeType = "Rectangle" | "RoundedRectangle" | "Circle" | "Oval"

export const EXPERIMENTAL_SHAPE_TYPES: ExperimentalShapeType[] = [
  "Rectangle",
  "RoundedRectangle",
  "Circle",
  "Oval",
]

export interface ShapeOutlineParams {
  cornerRadiusMm: number
  curveSegments: number
}

function rectangleOutline(width: number, height: number): THREE.Shape {
  const w = width / 2
  const h = height / 2
  const shape = new THREE.Shape()
  shape.moveTo(-w, -h)
  shape.lineTo(w, -h)
  shape.lineTo(w, h)
  shape.lineTo(-w, h)
  shape.closePath()
  return shape
}

function roundedRectangleOutline(width: number, height: number, params: ShapeOutlineParams): THREE.Shape {
  const w = width / 2
  const h = height / 2
  const r = Math.min(params.cornerRadiusMm, Math.min(w, h))
  const segmentsPerCorner = Math.max(2, Math.floor(params.curveSegments / 4))

  const points: THREE.Vector2[] = []
  const addCornerArc = (cx: number, cy: number, startDeg: number, endDeg: number) => {
    for (let i = 0; i <= segmentsPerCorner; i++) {
      const t = startDeg + ((endDeg - startDeg) * i) / segmentsPerCorner
      const rad = (t * Math.PI) / 180
      points.push(new THREE.Vector2(cx + r * Math.cos(rad), cy + r * Math.sin(rad)))
    }
  }

  addCornerArc(w - r, -(h - r), -90, 0)
  addCornerArc(w - r, h - r, 0, 90)
  addCornerArc(-(w - r), h - r, 90, 180)
  addCornerArc(-(w - r), -(h - r), 180, 270)

  const shape = new THREE.Shape(points)
  shape.closePath()
  return shape
}

function ovalOutline(width: number, height: number, params: ShapeOutlineParams): THREE.Shape {
  const rx = width / 2
  const ry = height / 2
  const segments = Math.max(8, params.curveSegments)

  const points: THREE.Vector2[] = []
  for (let i = 0; i < segments; i++) {
    const t = (i * 2 * Math.PI) / segments
    points.push(new THREE.Vector2(rx * Math.cos(t), ry * Math.sin(t)))
  }

  const shape = new THREE.Shape(points)
  shape.closePath()
  return shape
}

function circleOutline(width: number, height: number, params: ShapeOutlineParams): THREE.Shape {
  const diameter = Math.min(width, height)
  return ovalOutline(diameter, diameter, params)
}

export function getExperimentalShapeOutline(
  shapeType: ExperimentalShapeType,
  width: number,
  height: number,
  params: ShapeOutlineParams
): THREE.Shape {
  switch (shapeType) {
    case "Rectangle":
      return rectangleOutline(width, height)
    case "RoundedRectangle":
      return roundedRectangleOutline(width, height, params)
    case "Circle":
      return circleOutline(width, height, params)
    case "Oval":
      return ovalOutline(width, height, params)
  }
}
