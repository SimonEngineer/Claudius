import { useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { PublicApi } from '@/api/resources'
import { BookingType, TripStatus, TimelineEntryType } from '@/types'
import { Card, CardContent } from '@/components/ui/card'
import { MapPin, CalendarDays, Plane } from 'lucide-react'

const BOOKING_LABEL: Record<number, string> = {
  [BookingType.Flight]: 'Flight', [BookingType.Hotel]: 'Hotel', [BookingType.CarRental]: 'Car rental',
  [BookingType.Ticket]: 'Ticket', [BookingType.Other]: 'Other',
}
const STATUS_LABEL: Record<number, string> = { [TripStatus.Planning]: 'Planning', [TripStatus.Active]: 'Active', [TripStatus.Completed]: 'Completed' }

export function PublicTripPage() {
  const { slug = '' } = useParams()
  const { data: trip, isError } = useQuery({ queryKey: ['public-trip', slug], queryFn: () => PublicApi.getSharedTrip(slug) })

  if (isError) return <div className="mx-auto max-w-2xl p-6 text-center text-muted-foreground">This trip link is invalid or no longer shared.</div>
  if (!trip) return null

  return (
    <div className="mx-auto max-w-2xl p-4 md:p-6 flex flex-col gap-4">
      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <Plane className="h-4 w-4" /> Shared trip (read-only)
      </div>
      <div>
        <h1 className="text-2xl font-bold">{trip.name}</h1>
        <p className="text-muted-foreground">{trip.startDate} → {trip.endDate}</p>
        <span className="mt-1 inline-block rounded-full bg-secondary px-2 py-0.5 text-xs">{STATUS_LABEL[trip.status]}</span>
        {trip.description && <p className="mt-2 text-sm">{trip.description}</p>}
      </div>

      <div>
        <h2 className="text-lg font-semibold mb-2">Stops</h2>
        <div className="flex flex-col gap-2">
          {trip.stops.map((s) => (
            <Card key={s.id}>
              <CardContent className="p-3">
                <div className="font-medium flex items-center gap-1.5"><MapPin className="h-4 w-4 text-muted-foreground" /> {s.name}</div>
                <div className="text-sm text-muted-foreground">{s.arriveDate} → {s.departDate}</div>
              </CardContent>
            </Card>
          ))}
          {trip.stops.length === 0 && <p className="text-sm text-muted-foreground">No stops.</p>}
        </div>
      </div>

      <div>
        <h2 className="text-lg font-semibold mb-2">Bookings</h2>
        <div className="flex flex-col gap-2">
          {trip.bookings.map((b) => (
            <Card key={b.id}>
              <CardContent className="p-3">
                <div className="font-medium">{BOOKING_LABEL[b.type]} · {b.title}</div>
                <div className="text-sm text-muted-foreground">{b.startAt} → {b.endAt}</div>
              </CardContent>
            </Card>
          ))}
          {trip.bookings.length === 0 && <p className="text-sm text-muted-foreground">No bookings.</p>}
        </div>
      </div>

      <div>
        <h2 className="text-lg font-semibold mb-2">Journal</h2>
        <div className="flex flex-col gap-2">
          {[...trip.timeline].reverse().map((e) => (
            <Card key={e.id}>
              <CardContent className="p-3">
                <div className="text-xs text-muted-foreground flex items-center gap-1"><CalendarDays className="h-3 w-3" /> {new Date(e.capturedAt).toLocaleString()}</div>
                {e.type === TimelineEntryType.Url ? (
                  <a href={e.content ?? ''} target="_blank" rel="noreferrer" className="text-primary underline">{e.content}</a>
                ) : e.type === TimelineEntryType.Note ? (
                  <p>{e.content}</p>
                ) : (
                  <p className="text-sm text-muted-foreground">Photo/video (not shown in shared view)</p>
                )}
              </CardContent>
            </Card>
          ))}
          {trip.timeline.length === 0 && <p className="text-sm text-muted-foreground">No journal entries.</p>}
        </div>
      </div>
    </div>
  )
}
