import { Suspense } from "react"
import { Canvas } from "@react-three/fiber"
import { OrbitControls, Center, Bounds } from "@react-three/drei"
import * as THREE from "three"

export interface ExperimentalModelViewerProps {
  group: THREE.Group
  className?: string
}

export function ExperimentalModelViewer({ group, className }: ExperimentalModelViewerProps) {
  return (
    <div className={className} style={{ width: "100%", height: "100%", minHeight: 320 }}>
      <Canvas camera={{ position: [0, -80, 60], fov: 45 }} shadows>
        <color attach="background" args={["#1c1d22"]} />
        <ambientLight intensity={0.6} />
        <directionalLight position={[50, -50, 80]} intensity={1.2} castShadow />
        <Suspense fallback={null}>
          <Bounds fit clip observe margin={1.4}>
            <Center>
              <primitive object={group} />
            </Center>
          </Bounds>
        </Suspense>
        <OrbitControls makeDefault />
      </Canvas>
    </div>
  )
}
