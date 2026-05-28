import { useState, useEffect } from 'react'
import { Link } from 'react-router-dom'
import { Trash2, Bell } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent } from '@/components/ui/card'
import api from '@/lib/api'

function WishlistItem({ item, onRemove, onUpdateThreshold }) {
  const [threshold, setThreshold] = useState(String(item.minDiscountPercent))
  const [saving, setSaving] = useState(false)

  const coverUrl = item.coverImageId
    ? `https://images.igdb.com/igdb/image/upload/t_cover_small/${item.coverImageId}.jpg`
    : null

  async function handleBlur() {
    const val = parseFloat(threshold)
    if (isNaN(val) || val < 1 || val > 99 || val === item.minDiscountPercent) return
    setSaving(true)
    try {
      await api.patch(`/api/wishlist/${item.id}`, { minDiscountPercent: val })
      onUpdateThreshold(item.id, val)
    } finally {
      setSaving(false)
    }
  }

  const currentDiscount = item.currentBestDiscount
  const thresholdMet = currentDiscount !== null && currentDiscount >= item.minDiscountPercent

  return (
    <Card>
      <CardContent className="p-4 flex items-center gap-4">
        {coverUrl ? (
          <img src={coverUrl} alt={item.gameName} className="w-16 h-20 object-cover rounded shrink-0" />
        ) : (
          <div className="w-16 h-20 bg-muted rounded shrink-0" />
        )}

        <div className="flex-1 min-w-0">
          <Link to={`/games/${item.gameId}`} className="font-semibold hover:text-primary transition-colors line-clamp-1">
            {item.gameName}
          </Link>

          <div className="flex items-center gap-3 mt-2 flex-wrap">
            <div className="flex items-center gap-2">
              <Bell className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
              <span className="text-sm text-muted-foreground">Alert at</span>
              <div className="flex items-center gap-1">
                <Input
                  type="number"
                  min={1}
                  max={99}
                  value={threshold}
                  onChange={(e) => setThreshold(e.target.value)}
                  onBlur={handleBlur}
                  className="w-16 h-7 text-sm px-2"
                  disabled={saving}
                />
                <span className="text-sm text-muted-foreground">% off</span>
              </div>
            </div>

            {currentDiscount != null && (
              <Badge variant={thresholdMet ? 'success' : 'secondary'}>
                Currently {Math.round(currentDiscount)}% off
              </Badge>
            )}
          </div>

          {item.lastNotifiedUtc && (
            <p className="text-xs text-muted-foreground mt-1">
              Last notified: {new Date(item.lastNotifiedUtc).toLocaleDateString()}
            </p>
          )}
        </div>

        <Button
          variant="ghost"
          size="icon"
          className="text-muted-foreground hover:text-destructive shrink-0"
          onClick={() => onRemove(item.id)}
        >
          <Trash2 className="h-4 w-4" />
        </Button>
      </CardContent>
    </Card>
  )
}

export default function WishlistPage() {
  const [items, setItems] = useState([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    api.get('/api/wishlist')
      .then((r) => setItems(r.data))
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [])

  async function handleRemove(id) {
    await api.delete(`/api/wishlist/${id}`)
    setItems((prev) => prev.filter((i) => i.id !== id))
  }

  function handleUpdateThreshold(id, val) {
    setItems((prev) => prev.map((i) => i.id === id ? { ...i, minDiscountPercent: val } : i))
  }

  return (
    <main className="container py-8 max-w-2xl">
      <h1 className="text-3xl font-bold tracking-tight mb-2">My Wishlist</h1>
      <p className="text-muted-foreground mb-6">You'll get an email when a game hits your discount threshold.</p>

      {loading && <p className="text-muted-foreground">Loading…</p>}

      {!loading && items.length === 0 && (
        <div className="text-center py-16 text-muted-foreground space-y-3">
          <p className="text-lg">Your wishlist is empty.</p>
          <Button asChild variant="outline">
            <Link to="/">Browse Games</Link>
          </Button>
        </div>
      )}

      {!loading && items.length > 0 && (
        <div className="space-y-3">
          {items.map((item) => (
            <WishlistItem
              key={item.id}
              item={item}
              onRemove={handleRemove}
              onUpdateThreshold={handleUpdateThreshold}
            />
          ))}
        </div>
      )}
    </main>
  )
}
