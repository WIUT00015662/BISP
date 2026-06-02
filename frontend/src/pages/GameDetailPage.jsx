import { useState, useEffect } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { ArrowLeft, Heart, HeartOff, Lock } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { PriceTable } from '@/components/PriceTable'
import api from '@/lib/api'
import { getToken } from '@/lib/auth'

export default function GameDetailPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const [game, setGame] = useState(null)
  const [loading, setLoading] = useState(true)
  const [subStatus, setSubStatus] = useState(null)
  const [wishlist, setWishlist] = useState([])
  const [adding, setAdding] = useState(false)
  const [subscriptionChecking, setSubscriptionChecking] = useState(false)

  const isLoggedIn = !!getToken()

  useEffect(() => {
    api.get(`/api/games/${id}`).then((r) => setGame(r.data)).catch(() => navigate('/')).finally(() => setLoading(false))

    let cancelled = false

    if (isLoggedIn) {
      const pendingKey = 'bisp_subscription_pending'

      async function fetchStatus() {
        try {
          const r = await api.get('/api/subscriptions/status')
          if (!cancelled) setSubStatus(r.data)
          return r.data
        } catch {
          return null
        }
      }

      fetchStatus()

      const pendingStamp = Number(localStorage.getItem(pendingKey))
      if (!Number.isNaN(pendingStamp) && Date.now() - pendingStamp < 5 * 60 * 1000) {
        setSubscriptionChecking(true)
        setTimeout(async () => {
          if (cancelled) return
          const current = await fetchStatus()
          if (current?.hasActiveSubscription) {
            localStorage.removeItem(pendingKey)
          }
          if (!cancelled) setSubscriptionChecking(false)
        }, 3000)
      }

      api.get('/api/wishlist').then((r) => setWishlist(r.data)).catch(() => {})
    }

    return () => {
      cancelled = true
    }
  }, [id, isLoggedIn, navigate])

  const isWishlisted = wishlist.some((w) => w.gameId === id)
  const hasSubscription = subStatus?.hasActiveSubscription

  async function toggleWishlist() {
    if (subscriptionChecking) return
    if (!hasSubscription) { navigate('/subscription'); return }

    setAdding(true)
    try {
      if (isWishlisted) {
        const item = wishlist.find((w) => w.gameId === id)
        await api.delete(`/api/wishlist/${item.id}`)
        setWishlist((prev) => prev.filter((w) => w.gameId !== id))
      } else {
        await api.post('/api/wishlist', { gameId: id, minDiscountPercent: 20 })
        const r = await api.get('/api/wishlist')
        setWishlist(r.data)
      }
    } finally {
      setAdding(false)
    }
  }

  if (loading) {
    return (
      <main className="container py-8">
        <div className="animate-pulse space-y-4">
          <div className="h-8 w-32 bg-muted rounded" />
          <div className="flex gap-8">
            <div className="w-64 h-80 bg-muted rounded" />
            <div className="flex-1 space-y-3">
              <div className="h-10 bg-muted rounded w-3/4" />
              <div className="h-4 bg-muted rounded w-1/2" />
              <div className="h-32 bg-muted rounded" />
            </div>
          </div>
        </div>
      </main>
    )
  }

  if (!game) return null

  const coverUrl = game.coverImageId
    ? `https://images.igdb.com/igdb/image/upload/t_cover_big/${game.coverImageId}.jpg`
    : null

  return (
    <main className="container py-8">
      <Button variant="ghost" size="sm" onClick={() => navigate(-1)} className="mb-6 -ml-2">
        <ArrowLeft className="h-4 w-4 mr-1" /> Back
      </Button>

      <div className="flex flex-col md:flex-row gap-8">
        {/* Cover */}
        <div className="shrink-0">
          {coverUrl ? (
            <img src={coverUrl} alt={game.name} className="w-64 rounded-lg shadow-lg" />
          ) : (
            <div className="w-64 h-80 rounded-lg bg-muted flex items-center justify-center text-muted-foreground">
              No cover
            </div>
          )}
        </div>

        {/* Info */}
        <div className="flex-1 space-y-4">
          <div>
            <h1 className="text-3xl font-bold tracking-tight">{game.name}</h1>
            <div className="flex flex-wrap gap-1.5 mt-2">
              {game.genres?.map((g) => <Badge key={g} variant="secondary">{g}</Badge>)}
            </div>
          </div>

          {game.summary && (
            <p className="text-muted-foreground leading-relaxed max-w-2xl">{game.summary}</p>
          )}

          {game.bestCurrentPrice != null && (
            <div className="flex items-baseline gap-3">
              <span className="text-4xl font-bold text-primary">${game.bestCurrentPrice.toFixed(2)}</span>
              {game.bestRegularPrice && game.bestRegularPrice !== game.bestCurrentPrice && (
                <span className="text-xl text-muted-foreground line-through">${game.bestRegularPrice.toFixed(2)}</span>
              )}
              {game.bestDiscountPercent > 0 && (
                <Badge variant={game.bestDiscountPercent >= 20 ? 'success' : 'warning'} className="text-base px-3 py-1">
                  -{Math.round(game.bestDiscountPercent)}% off
                </Badge>
              )}
            </div>
          )}

          {/* Wishlist button */}
          {isLoggedIn ? (
            <Button
              onClick={toggleWishlist}
              variant={isWishlisted ? 'outline' : 'default'}
              disabled={adding || subscriptionChecking}
            >
              {isWishlisted ? (
                <><HeartOff className="h-4 w-4 mr-2" /> Remove from Wishlist</>
              ) : subscriptionChecking ? (
                <><Lock className="h-4 w-4 mr-2" /> Confirming Subscription...</>
              ) : hasSubscription ? (
                <><Heart className="h-4 w-4 mr-2" /> Add to Wishlist</>
              ) : (
                <><Lock className="h-4 w-4 mr-2" /> Subscribe to Add Wishlist</>
              )}
            </Button>
          ) : (
            <Button asChild variant="outline">
              <Link to="/auth/login"><Heart className="h-4 w-4 mr-2" /> Login to Add Wishlist</Link>
            </Button>
          )}
        </div>
      </div>

      {/* Price comparison */}
      <div className="mt-10">
        <h2 className="text-xl font-semibold mb-4">Price Comparison</h2>
        <PriceTable prices={game.prices} />
      </div>
    </main>
  )
}
