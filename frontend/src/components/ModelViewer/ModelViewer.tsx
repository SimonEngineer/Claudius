import { Suspense } from "react"
import { Canvas, useLoader } from "@react-three/fiber"
import { OrbitControls, Center, Bounds } from "@react-three/drei"
import { STLLoader } from "three/examples/jsm/loaders/STLLoader.js"

function StlMesh({ url }: { url: string }) {
  const geometry = useLoader(STLLoader, url)
  return (
    <mesh geometry={geometry} castShadow receiveShadow>
      <meshStandardMaterial color="#c7c2b8" roughness={0.6} metalness={0.1} />
    </mesh>
  )
}

function Loading() {
  return (
    <mesh>
      <boxGeometry args={[1, 1, 1]} />
      <meshBasicMaterial color="#ddd" wireframe />
    </mesh>
  )
}

export interface ModelViewerProps {
  /** URL serving the STL bytes (e.g. a preview endpoint). */
  stlUrl: string
  className?: string
}

export function ModelViewer({ stlUrl, className }: ModelViewerProps) {
  return (
    <div className={className} style={{ width: "100%", height: "100%", minHeight: 320 }}>
      <Canvas camera={{ position: [0, -80, 60], fov: 45 }} shadows>
        <color attach="background" args={["#1c1d22"]} />
        <ambientLight intensity={0.6} />
        <directionalLight position={[50, -50, 80]} intensity={1.2} castShadow />
        <Suspense fallback={<Loading />}>
          <Bounds fit clip observe margin={1.4}>
            <Center>
              <StlMesh url={stlUrl} />
            </Center>
          </Bounds>
        </Suspense>
        <OrbitControls makeDefault />
      </Canvas>
    </div>
  )
}
