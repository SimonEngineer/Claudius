import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { LocationsApi, GoalsApi, TripsApi } from '@/api/resources'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { MapPinned, Target, Plane, CalendarClock, Globe2, BedDouble, CheckCircle2, FileWarning } from 'lucide-react'
import { DocumentType } from '@/types'

const docTypeLabels: Record<number, string> = {
  [DocumentType.Passport]: 'Passport',
  [DocumentType.Visa]: 'Visa',
  [DocumentType.Insurance]: 'Insurance',
  [DocumentType.BookingConfirmation]: 'Booking confirmation',
  [DocumentType.Other]: 'Document',
}

export function DashboardPage() {
  const { data: locations = [] } = useQuery({ queryKey: ['locations'], queryFn: LocationsApi.list })
  const { data: goals = [] } = useQuery({ queryKey: ['goals'], queryFn: GoalsApi.list })
  const { data: trips = [] } = useQuery({ queryKey: ['trips'], queryFn: TripsApi.list })
  const { data: stats } = useQuery({ queryKey: ['trip-stats'], queryFn: TripsApi.stats })
  const { data: upcomingDocuments = [] } = useQuery({ queryKey: ['upcoming-documents'], queryFn: TripsApi.upcomingDocuments })

  const upcomingTrips = trips.filter((t) => t.status !== 3).slice(0, 3)

  const today = new Date(); today.setHours(0, 0, 0, 0)
  const nextTrip = trips
    .filter((t) => t.startDate && new Date(t.startDate) >= today)
    .sort((a, b) => new Date(a.startDate!).getTime() - new Date(b.startDate!).getTime())[0]
  const daysToGo = nextTrip ? Math.ceil((new Date(nextTrip.startDate!).getTime() - today.getTime()) / 86400000) : null

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-bold">Welcome back</h1>

      {nextTrip ? (
        <Link to={`/trips/${nextTrip.id}`}>
          <Card className="hover:shadow-md transition-shadow">
            <CardContent className="flex items-center gap-3 p-4">
              <CalendarClock className="h-8 w-8 text-primary" />
              <div>
                <div className="text-sm text-muted-foreground">Next trip</div>
                <div className="text-lg font-semibold">{nextTrip.name}</div>
                <div className="text-sm text-muted-foreground">
                  {daysToGo === 0 ? 'Starts today!' : `${daysToGo} day${daysToGo === 1 ? '' : 's'} to go`}
                </div>
              </div>
            </CardContent>
          </Card>
        </Link>
      ) : (
        <Card><CardContent className="p-4 text-sm text-muted-foreground">No upcoming trips with a start date set.</CardContent></Card>
      )}

      {stats && (
        <section>
          <h2 className="mb-2 text-lg font-semibold">Travel stats</h2>
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
            <Card><CardContent className="flex items-center gap-3 p-4">
              <CheckCircle2 className="h-6 w-6 text-primary" />
              <div><div className="text-xl font-bold">{stats.completedTrips}</div><div className="text-xs text-muted-foreground">Trips completed</div></div>
            </CardContent></Card>
            <Card><CardContent className="flex items-center gap-3 p-4">
              <Globe2 className="h-6 w-6 text-primary" />
              <div><div className="text-xl font-bold">{stats.countriesVisited}</div><div className="text-xs text-muted-foreground">Countries visited</div></div>
            </CardContent></Card>
            <Card><CardContent className="flex items-center gap-3 p-4">
              <BedDouble className="h-6 w-6 text-primary" />
              <div><div className="text-xl font-bold">{stats.totalNights}</div><div className="text-xs text-muted-foreground">Nights away</div></div>
            </CardContent></Card>
            <Card><CardContent className="flex items-center gap-3 p-4">
              <Plane className="h-6 w-6 text-primary" />
              <div><div className="text-xl font-bold">{stats.upcomingTrips}</div><div className="text-xs text-muted-foreground">Upcoming trips</div></div>
            </CardContent></Card>
          </div>
        </section>
      )}

      {upcomingDocuments.length > 0 && (
        <section>
          <h2 className="mb-2 text-lg font-semibold">Documents expiring soon</h2>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            {upcomingDocuments.map((d) => (
              <Link to={`/trips/${d.tripId}`} key={d.id}>
                <Card>
                  <CardContent className="flex items-center gap-3 p-4">
                    <FileWarning className="h-5 w-5 text-destructive shrink-0" />
                    <div>
                      <div className="font-medium">{d.title} <span className="text-xs text-muted-foreground">({docTypeLabels[d.docType] ?? 'Document'})</span></div>
                      <div className="text-sm text-muted-foreground">Expires {d.expiryDate} · {d.tripName}</div>
                    </div>
                  </CardContent>
                </Card>
              </Link>
            ))}
          </div>
        </section>
      )}

      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <Link to="/wishlist">
          <Card className="hover:shadow-md transition-shadow">
            <CardHeader className="flex-row items-center gap-2 space-y-0">
              <MapPinned className="h-5 w-5 text-primary" />
              <CardTitle className="text-base">Wishlist</CardTitle>
            </CardHeader>
            <CardContent className="text-2xl font-bold">{locations.length}</CardContent>
          </Card>
        </Link>
        <Link to="/goals">
          <Card className="hover:shadow-md transition-shadow">
            <CardHeader className="flex-row items-center gap-2 space-y-0">
              <Target className="h-5 w-5 text-primary" />
              <CardTitle className="text-base">Goals</CardTitle>
            </CardHeader>
            <CardContent className="text-2xl font-bold">{goals.length}</CardContent>
          </Card>
        </Link>
        <Link to="/trips">
          <Card className="hover:shadow-md transition-shadow">
            <CardHeader className="flex-row items-center gap-2 space-y-0">
              <Plane className="h-5 w-5 text-primary" />
              <CardTitle className="text-base">Trips</CardTitle>
            </CardHeader>
            <CardContent className="text-2xl font-bold">{trips.length}</CardContent>
          </Card>
        </Link>
      </div>

      <section>
        <h2 className="mb-2 text-lg font-semibold">Goal progress</h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          {goals.map((g) => (
            <Link to={`/goals/${g.id}`} key={g.id}>
              <Card>
                <CardContent className="flex items-center justify-between p-4">
                  <span className="font-medium">{g.icon} {g.name}</span>
                  <span className="text-sm text-muted-foreground">{g.completedCount}/{g.itemCount}</span>
                </CardContent>
              </Card>
            </Link>
          ))}
          {goals.length === 0 && <p className="text-sm text-muted-foreground">No goals yet.</p>}
        </div>
      </section>

      <section>
        <h2 className="mb-2 text-lg font-semibold">Upcoming trips</h2>
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
          {upcomingTrips.map((t) => (
            <Link to={`/trips/${t.id}`} key={t.id}>
              <Card>
                <CardContent className="p-4">
                  <div className="font-medium">{t.name}</div>
                  <div className="text-sm text-muted-foreground">{t.startDate} → {t.endDate}</div>
                </CardContent>
              </Card>
            </Link>
          ))}
          {upcomingTrips.length === 0 && <p className="text-sm text-muted-foreground">No upcoming trips.</p>}
        </div>
      </section>
    </div>
  )
}
