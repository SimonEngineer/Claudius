import { MapContainer, TileLayer, Marker, Popup, useMapEvents } from 'react-leaflet'
import L from 'leaflet'

// Default Leaflet marker icons reference image files Vite won't resolve via CSS url(); wire them up explicitly.
import markerIcon from 'leaflet/dist/images/marker-icon.png'
import markerIcon2x from 'leaflet/dist/images/marker-icon-2x.png'
import markerShadow from 'leaflet/dist/images/marker-shadow.png'

L.Icon.Default.mergeOptions({
  iconUrl: markerIcon,
  iconRetinaUrl: markerIcon2x,
  shadowUrl: markerShadow,
})

export interface MapPin {
  id: string
  lat: number
  lng: number
  label: string
  color?: string
}

interface LocationMapProps {
  pins: MapPin[]
  height?: number | string
  center?: [number, number]
  zoom?: number
  onPick?: (lat: number, lng: number) => void
  onPinClick?: (id: string) => void
}

function ClickHandler({ onPick }: { onPick?: (lat: number, lng: number) => void }) {
  useMapEvents({
    click(e) {
      onPick?.(e.latlng.lat, e.latlng.lng)
    },
  })
  return null
}

export function LocationMap({ pins, height = 320, center, zoom = 2, onPick, onPinClick }: LocationMapProps) {
  const defaultCenter: [number, number] = center ?? (pins[0] ? [pins[0].lat, pins[0].lng] : [20, 0])

  return (
    <div style={{ height }} className="relative isolate overflow-hidden rounded-md border">
      <MapContainer center={defaultCenter} zoom={zoom} style={{ height: '100%', width: '100%' }}>
        <TileLayer
          attribution='&copy; OpenStreetMap contributors'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        <ClickHandler onPick={onPick} />
        {pins.map((pin) => (
          <Marker key={pin.id} position={[pin.lat, pin.lng]} eventHandlers={{ click: () => onPinClick?.(pin.id) }}>
            <Popup>{pin.label}</Popup>
          </Marker>
        ))}
      </MapContainer>
    </div>
  )
}
