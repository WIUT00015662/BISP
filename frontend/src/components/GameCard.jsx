import { Link } from 'react-router-dom'
import { ExternalLink } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent } from '@/components/ui/card'

const STORE_LABELS = {
  steam: 'Steam',
  gog: 'GOG',
  epic: 'Epic Games'
}

function DiscountBadge({ percent }) {
  if (!percent || percent <= 0) return null
  const variant = percent >= 20 ? 'success' : 'warning'
  return <Badge variant={variant}>-{Math.round(percent)}%</Badge>
}

export function GameCard({ game }) {
  const coverUrl = game.coverImageId
    ? `https://images.igdb.com/igdb/image/upload/t_cover_big/${game.coverImageId}.jpg`
    : null

  return (
    <Link to={`/games/${game.id}`} className="group">
      <Card className="overflow-hidden transition-all duration-200 hover:shadow-lg hover:-translate-y-0.5 h-full flex flex-col">
        <div className="relative aspect-[3/4] bg-muted overflow-hidden">
          {coverUrl ? (
            <img
              src={coverUrl}
              alt={game.name}
              className="w-full h-full object-cover transition-transform duration-300 group-hover:scale-105"
              loading="lazy"
            />
          ) : (
            <div className="w-full h-full flex items-center justify-center text-muted-foreground text-sm">
              No cover
            </div>
          )}
          {game.bestDiscountPercent > 0 && (
            <div className="absolute top-2 right-2">
              <DiscountBadge percent={game.bestDiscountPercent} />
            </div>
          )}
        </div>

        <CardContent className="p-3 flex flex-col gap-1 flex-1">
          <h3 className="font-semibold text-sm leading-tight line-clamp-2 group-hover:text-primary transition-colors">
            {game.name}
          </h3>

          {game.genres?.length > 0 && (
            <div className="flex flex-wrap gap-1">
              {game.genres.slice(0, 2).map((g) => (
                <span key={g} className="text-xs text-muted-foreground bg-muted px-1.5 py-0.5 rounded">
                  {g}
                </span>
              ))}
            </div>
          )}

          <div className="mt-auto pt-2">
            {game.bestCurrentPrice != null ? (
              <div className="flex items-baseline gap-2">
                <span className="font-bold text-primary">${game.bestCurrentPrice.toFixed(2)}</span>
                {game.bestRegularPrice && game.bestRegularPrice !== game.bestCurrentPrice && (
                  <span className="text-xs text-muted-foreground line-through">
                    ${game.bestRegularPrice.toFixed(2)}
                  </span>
                )}
              </div>
            ) : (
              <span className="text-sm text-muted-foreground">Price unavailable</span>
            )}
            <div className="flex items-center justify-between gap-2 mt-0.5">
              <p className="text-xs text-muted-foreground">
                {game.storeCount} store{game.storeCount !== 1 ? 's' : ''}
              </p>
              {game.bestStoreUrl && (
                <button
                  type="button"
                  onClick={(event) => {
                    event.preventDefault()
                    event.stopPropagation()
                    window.open(game.bestStoreUrl, '_blank', 'noreferrer')
                  }}
                  className="text-xs text-muted-foreground hover:text-primary inline-flex items-center gap-1"
                  aria-label={`Open ${STORE_LABELS[game.bestStoreCode] || 'store'} page`}
                >
                  {STORE_LABELS[game.bestStoreCode] || 'Store'}
                  <ExternalLink className="h-3 w-3" />
                </button>
              )}
            </div>
          </div>
        </CardContent>
      </Card>
    </Link>
  )
}
