import * as THREE from "three"
import * as opentype from "opentype.js"
import { getExperimentalShapeOutline, type ExperimentalShapeType } from "./shapeOutlines"

const NOMINAL_TEXT_SIZE_MM = 10

let fontPromise: Promise<opentype.Font> | null = null

/**
 * DejaVuSans-Bold.ttf ships a GSUB lookup-type-6 format-2 subtable that
 * opentype.js 2.0's parser doesn't support, which throws and aborts parsing
 * entirely. GSUB only matters for ligature/contextual substitution, neither
 * of which this page needs (plain per-character glyph outlines via
 * font.getPath) -- so disable it by renaming the table's directory tag,
 * which makes the parser skip it instead of attempting to read it.
 */
function disableGsubTable(buffer: ArrayBuffer): ArrayBuffer {
  const view = new DataView(buffer)
  const numTables = view.getUint16(4)
  const decoder = new TextDecoder("ascii")
  for (let i = 0; i < numTables; i++) {
    const recordOffset = 12 + i * 16
    const tagBytes = new Uint8Array(buffer, recordOffset, 4)
    if (decoder.decode(tagBytes) === "GSUB") {
      view.setUint8(recordOffset, 0)
    }
  }
  return buffer
}

export function loadExperimentalFont(): Promise<opentype.Font> {
  fontPromise ??= fetch("/fonts/DejaVuSans-Bold.ttf")
    .then((response) => response.arrayBuffer())
    .then((buffer) => opentype.parse(disableGsubTable(buffer)))
  return fontPromise
}

/**
 * opentype.js getPath() returns coordinates in the same y-down convention as a
 * canvas 2D context (it's designed to be fed straight into ctx.moveTo/lineTo);
 * three.js Shapes are y-up. Negate y on every point, mirroring the same flip
 * the backend's SkiaGlyphOutlineProvider applies for the same reason.
 */
function opentypePathToShapePath(path: opentype.Path): THREE.ShapePath {
  const shapePath = new THREE.ShapePath()
  for (const cmd of path.commands) {
    switch (cmd.type) {
      case "M":
        shapePath.moveTo(cmd.x, -cmd.y)
        break
      case "L":
        shapePath.lineTo(cmd.x, -cmd.y)
        break
      case "C":
        shapePath.bezierCurveTo(cmd.x1, -cmd.y1, cmd.x2, -cmd.y2, cmd.x, -cmd.y)
        break
      case "Q":
        shapePath.quadraticCurveTo(cmd.x1, -cmd.y1, cmd.x, -cmd.y)
        break
      case "Z":
        break
    }
  }
  return shapePath
}

function buildTextShapes(font: opentype.Font, text: string): THREE.Shape[] {
  if (text.length === 0) return []
  const path = font.getPath(text, 0, 0, NOMINAL_TEXT_SIZE_MM)
  const shapePath = opentypePathToShapePath(path)
  return shapePath.toShapes(false) as THREE.Shape[]
}

function getShapesBounds(shapes: THREE.Shape[]): THREE.Box2 {
  const box = new THREE.Box2(new THREE.Vector2(Infinity, Infinity), new THREE.Vector2(-Infinity, -Infinity))
  for (const shape of shapes) {
    for (const point of shape.getPoints()) {
      box.expandByPoint(point)
    }
  }
  return box
}

/** Translates+uniformly scales shapes so their bounds fit targetWidth/targetHeight, centered at the origin. */
function fitShapesToSize(shapes: THREE.Shape[], targetWidth: number, targetHeight: number): THREE.Shape[] {
  const bounds = getShapesBounds(shapes)
  if (!Number.isFinite(bounds.min.x)) return shapes

  const size = bounds.getSize(new THREE.Vector2())
  const scale = Math.min(size.x > 0 ? targetWidth / size.x : 1, size.y > 0 ? targetHeight / size.y : 1)
  const center = bounds.getCenter(new THREE.Vector2())

  return shapes.map((shape) => {
    const fitted = new THREE.Shape(
      shape.getPoints().map((p) => p.clone().sub(center).multiplyScalar(scale))
    )
    fitted.holes = shape.holes.map(
      (hole) => new THREE.Path(hole.getPoints().map((p) => p.clone().sub(center).multiplyScalar(scale)))
    )
    return fitted
  })
}

export interface ExperimentalMeshParams {
  shapeType: ExperimentalShapeType
  plateWidthMm: number
  plateHeightMm: number
  plateThicknessMm: number
  textDepthMm: number
  marginMm: number
  cornerRadiusMm: number
  curveSegments: number
}

export interface ExperimentalMesh {
  group: THREE.Group
  plateGeometry: THREE.BufferGeometry
  textGeometry: THREE.BufferGeometry | null
}

export function buildExperimentalMesh(font: opentype.Font, text: string, params: ExperimentalMeshParams): ExperimentalMesh {
  const plateShape = getExperimentalShapeOutline(params.shapeType, params.plateWidthMm, params.plateHeightMm, {
    cornerRadiusMm: params.cornerRadiusMm,
    curveSegments: params.curveSegments,
  })
  const plateGeometry = new THREE.ExtrudeGeometry(plateShape, {
    depth: params.plateThicknessMm,
    bevelEnabled: false,
  })

  const material = new THREE.MeshStandardMaterial({ color: "#c7c2b8", roughness: 0.6, metalness: 0.1 })

  const group = new THREE.Group()
  const plateMesh = new THREE.Mesh(plateGeometry, material)
  group.add(plateMesh)

  const availableWidth = Math.max(1, params.plateWidthMm - 2 * params.marginMm)
  const availableHeight = Math.max(1, params.plateHeightMm - 2 * params.marginMm)

  const rawShapes = buildTextShapes(font, text)
  let textGeometry: THREE.BufferGeometry | null = null
  if (rawShapes.length > 0) {
    const fittedShapes = fitShapesToSize(rawShapes, availableWidth, availableHeight)
    textGeometry = new THREE.ExtrudeGeometry(fittedShapes, {
      depth: params.textDepthMm,
      bevelEnabled: false,
    })
    textGeometry.translate(0, 0, params.plateThicknessMm)
    const textMesh = new THREE.Mesh(textGeometry, material)
    group.add(textMesh)
  }

  return { group, plateGeometry, textGeometry }
}
