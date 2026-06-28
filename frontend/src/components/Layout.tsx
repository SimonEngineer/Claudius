import { NavLink, Outlet } from 'react-router-dom'
import { LayoutDashboard, MapPinned, Target, Plane, Tags } from 'lucide-react'
import { cn } from '@/lib/utils'
import { QuickCapture } from '@/components/QuickCapture'
import { OfflineBanner } from '@/components/OfflineBanner'

const navItems = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard },
  { to: '/wishlist', label: 'Wishlist', icon: MapPinned },
  { to: '/goals', label: 'Goals', icon: Target },
  { to: '/trips', label: 'Trips', icon: Plane },
  { to: '/tags', label: 'Tags', icon: Tags },
]

export function Layout() {
  return (
    <div className="flex min-h-svh w-full flex-col">
      <OfflineBanner />
    <div className="flex flex-1 w-full">
      <aside className="hidden md:flex w-56 shrink-0 flex-col border-r bg-card p-4 gap-1">
        <div className="px-2 pb-4 text-lg font-bold">Wanderlist</div>
        {navItems.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            className={({ isActive }) =>
              cn(
                'flex items-center gap-2 rounded-md px-3 py-2 text-sm font-medium hover:bg-accent',
                isActive && 'bg-accent text-primary'
              )
            }
          >
            <item.icon className="h-4 w-4" />
            {item.label}
          </NavLink>
        ))}
      </aside>

      <main className="flex-1 min-h-svh pb-16 md:pb-0">
        <div className="mx-auto max-w-6xl p-4 md:p-6">
          <Outlet />
        </div>
      </main>

      <nav className="fixed bottom-0 left-0 right-0 z-40 flex md:hidden border-t bg-card">
        {navItems.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            className={({ isActive }) =>
              cn(
                'flex flex-1 flex-col items-center gap-0.5 py-2 text-xs font-medium text-muted-foreground',
                isActive && 'text-primary'
              )
            }
          >
            <item.icon className="h-5 w-5" />
            {item.label}
          </NavLink>
        ))}
      </nav>

      <QuickCapture />
    </div>
    </div>
  )
}
