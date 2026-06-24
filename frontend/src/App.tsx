import { lazy, Suspense } from 'react'
import { Routes, Route } from 'react-router-dom'
import { Layout } from '@/components/Layout'

const DashboardPage = lazy(() => import('@/pages/DashboardPage').then((m) => ({ default: m.DashboardPage })))
const WishlistPage = lazy(() => import('@/pages/WishlistPage').then((m) => ({ default: m.WishlistPage })))
const WishlistDetailPage = lazy(() => import('@/pages/WishlistDetailPage').then((m) => ({ default: m.WishlistDetailPage })))
const GoalsPage = lazy(() => import('@/pages/GoalsPage').then((m) => ({ default: m.GoalsPage })))
const GoalDetailPage = lazy(() => import('@/pages/GoalDetailPage').then((m) => ({ default: m.GoalDetailPage })))
const TripsPage = lazy(() => import('@/pages/TripsPage').then((m) => ({ default: m.TripsPage })))
const TripDetailPage = lazy(() => import('@/pages/TripDetailPage').then((m) => ({ default: m.TripDetailPage })))
const TagsPage = lazy(() => import('@/pages/TagsPage').then((m) => ({ default: m.TagsPage })))

export default function App() {
  return (
    <Suspense fallback={<div className="p-6 text-sm text-muted-foreground">Loading…</div>}>
      <Routes>
        <Route element={<Layout />}>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/wishlist" element={<WishlistPage />} />
          <Route path="/wishlist/:id" element={<WishlistDetailPage />} />
          <Route path="/goals" element={<GoalsPage />} />
          <Route path="/goals/:id" element={<GoalDetailPage />} />
          <Route path="/trips" element={<TripsPage />} />
          <Route path="/trips/:id" element={<TripDetailPage />} />
          <Route path="/tags" element={<TagsPage />} />
        </Route>
      </Routes>
    </Suspense>
  )
}
