import { useState, useEffect, useCallback } from 'react'
import { Search, SlidersHorizontal } from 'lucide-react'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { GameCard } from '@/components/GameCard'
import api from '@/lib/api'

const GENRES = ['Action', 'Adventure', 'RPG', 'Strategy', 'Shooter', 'Sports', 'Racing', 'Simulation', 'Puzzle', 'Indie']
const STORES = [
  { value: 'steam', label: 'Steam' },
  { value: 'gog', label: 'GOG' },
  { value: 'epic', label: 'Epic Games' },
]

function useDebounce(value, delay) {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => {
    const t = setTimeout(() => setDebounced(value), delay)
    return () => clearTimeout(t)
  }, [value, delay])
  return debounced
}

export default function HomePage() {
  const [search, setSearch] = useState('')
  const [genre, setGenre] = useState('all')
  const [store, setStore] = useState('all')
  const [page, setPage] = useState(1)
  const [data, setData] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  const debouncedSearch = useDebounce(search, 300)

  const fetchGames = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const params = { page, pageSize: 12 }
      if (debouncedSearch) params.search = debouncedSearch
      if (genre !== 'all') params.genre = genre
      if (store !== 'all') params.store = store

      const res = await api.get('/api/games', { params })
      setData(res.data)
    } catch {
      setError('Failed to load games.')
    } finally {
      setLoading(false)
    }
  }, [page, debouncedSearch, genre, store])

  useEffect(() => {
    fetchGames()
  }, [fetchGames])

  // Reset to page 1 when filters change
  useEffect(() => {
    setPage(1)
  }, [debouncedSearch, genre, store])

  return (
    <main className="container py-8">
      <div className="mb-8">
        <h1 className="text-3xl font-bold tracking-tight mb-1">Game Price Tracker</h1>
        <p className="text-muted-foreground">Compare game prices across Steam, GOG, and Epic Games.</p>
      </div>

      {/* Filters */}
      <div className="flex flex-col sm:flex-row gap-3 mb-6">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Search games..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="pl-9"
          />
        </div>

        <div className="flex gap-2 items-center">
          <SlidersHorizontal className="h-4 w-4 text-muted-foreground shrink-0" />
          <Select value={genre} onValueChange={setGenre}>
            <SelectTrigger className="w-40">
              <SelectValue placeholder="Genre" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All Genres</SelectItem>
              {GENRES.map((g) => <SelectItem key={g} value={g}>{g}</SelectItem>)}
            </SelectContent>
          </Select>

          <Select value={store} onValueChange={setStore}>
            <SelectTrigger className="w-40">
              <SelectValue placeholder="Store" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All Stores</SelectItem>
              {STORES.map((s) => <SelectItem key={s.value} value={s.value}>{s.label}</SelectItem>)}
            </SelectContent>
          </Select>
        </div>
      </div>

      {/* Results */}
      {error && <p className="text-destructive text-center py-8">{error}</p>}

      {loading && (
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-4">
          {Array.from({ length: 12 }).map((_, i) => (
            <div key={i} className="rounded-lg border bg-muted animate-pulse aspect-[3/5]" />
          ))}
        </div>
      )}

      {!loading && !error && data && (
        <>
          {data.items.length === 0 ? (
            <div className="text-center py-16 text-muted-foreground">
              <p className="text-lg">No games found.</p>
              <p className="text-sm mt-1">Try adjusting your search or filters.</p>
            </div>
          ) : (
            <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4">
              {data.items.map((game) => <GameCard key={game.id} game={game} />)}
            </div>
          )}

          {/* Pagination */}
          {data.totalPages > 1 && (
            <div className="flex items-center justify-center gap-2 mt-8">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => p - 1)}
                disabled={page === 1}
              >
                Previous
              </Button>

              <div className="flex gap-1">
                {Array.from({ length: Math.min(data.totalPages, 7) }, (_, i) => {
                  const p = i + 1
                  return (
                    <Button
                      key={p}
                      variant={page === p ? 'default' : 'outline'}
                      size="sm"
                      onClick={() => setPage(p)}
                      className="w-9"
                    >
                      {p}
                    </Button>
                  )
                })}
              </div>

              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => p + 1)}
                disabled={page === data.totalPages}
              >
                Next
              </Button>
            </div>
          )}

          <p className="text-center text-sm text-muted-foreground mt-4">
            Showing {data.items.length} of {data.totalCount} games
          </p>
        </>
      )}
    </main>
  )
}
