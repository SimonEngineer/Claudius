import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Search, X } from 'lucide-react'
import { SearchApi } from '@/api/resources'
import { Input } from '@/components/ui/input'

const typeRoutes: Record<string, string> = {
  Trip: '/trips',
  Wishlist: '/wishlist',
  Goal: '/goals',
}

export function GlobalSearch() {
  const [q, setQ] = useState('')
  const [open, setOpen] = useState(false)
  const navigate = useNavigate()
  const containerRef = useRef<HTMLDivElement>(null)

  const { data: results = [] } = useQuery({
    queryKey: ['search', q],
    queryFn: () => SearchApi.search(q),
    enabled: q.trim().length >= 2,
  })

  useEffect(() => {
    function onClickOutside(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', onClickOutside)
    return () => document.removeEventListener('mousedown', onClickOutside)
  }, [])

  return (
    <div ref={containerRef} className="relative w-full max-w-sm">
      <div className="relative">
        <Search className="absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          value={q}
          onChange={(e) => { setQ(e.target.value); setOpen(true) }}
          onFocus={() => setOpen(true)}
          placeholder="Search trips, wishlist, goals…"
          className="pl-8 pr-8"
        />
        {q && (
          <button
            type="button"
            onClick={() => { setQ(''); setOpen(false) }}
            className="absolute right-2.5 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
          >
            <X className="h-4 w-4" />
          </button>
        )}
      </div>
      {open && q.trim().length >= 2 && (
        <div className="absolute z-50 mt-1 w-full rounded-md border bg-card shadow-lg max-h-80 overflow-y-auto">
          {results.length === 0 ? (
            <div className="px-3 py-2 text-sm text-muted-foreground">No results</div>
          ) : (
            results.map((r) => (
              <button
                key={`${r.type}-${r.id}`}
                type="button"
                onClick={() => {
                  setOpen(false)
                  setQ('')
                  navigate(`${typeRoutes[r.type]}/${r.id}`)
                }}
                className="flex w-full flex-col items-start px-3 py-2 text-left text-sm hover:bg-accent"
              >
                <span className="font-medium">{r.title}</span>
                <span className="text-xs text-muted-foreground">{r.type}{r.subtitle ? ` · ${r.subtitle}` : ''}</span>
              </button>
            ))
          )}
        </div>
      )}
    </div>
  )
}
