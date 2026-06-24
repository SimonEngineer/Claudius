import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { LocationsApi, GoalsApi, TripsApi } from '@/api/resources'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { MapPinned, Target, Plane, CalendarClock } from 'lucide-react'

export function DashboardPage() {
  const { data: locations = [] } = useQuery({ queryKey: ['locations'], queryFn: LocationsApi.list })
  const { data: goals = [] } = useQuery({ queryKey: ['goals'], queryFn: GoalsApi.list })
  const { data: trips = [] } = useQuery({ queryKey: ['trips'], queryFn: TripsApi.list })

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
