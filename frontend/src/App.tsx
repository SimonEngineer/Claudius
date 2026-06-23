import { Routes, Route } from 'react-router-dom'
import { Layout } from '@/components/Layout'
import { DashboardPage } from '@/pages/DashboardPage'
import { WishlistPage } from '@/pages/WishlistPage'
import { WishlistDetailPage } from '@/pages/WishlistDetailPage'
import { GoalsPage } from '@/pages/GoalsPage'
import { GoalDetailPage } from '@/pages/GoalDetailPage'
import { TripsPage } from '@/pages/TripsPage'
import { TripDetailPage } from '@/pages/TripDetailPage'

export default function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/" element={<DashboardPage />} />
        <Route path="/wishlist" element={<WishlistPage />} />
        <Route path="/wishlist/:id" element={<WishlistDetailPage />} />
        <Route path="/goals" element={<GoalsPage />} />
        <Route path="/goals/:id" element={<GoalDetailPage />} />
        <Route path="/trips" element={<TripsPage />} />
        <Route path="/trips/:id" element={<TripDetailPage />} />
      </Route>
    </Routes>
  )
}
