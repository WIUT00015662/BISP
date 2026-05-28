import { Badge } from '@/components/ui/badge'

const STORES = [
  { code: 'steam', name: 'Steam' },
  { code: 'gog', name: 'GOG' },
  { code: 'epic', name: 'Epic Games' },
]

function DiscountCell({ percent }) {
  if (!percent || percent <= 0) return <span className="text-muted-foreground">—</span>
  const variant = percent >= 20 ? 'success' : 'warning'
  return <Badge variant={variant}>-{Math.round(percent)}%</Badge>
}

export function PriceTable({ prices }) {
  const priceMap = Object.fromEntries((prices || []).map((p) => [p.storeCode, p]))

  return (
    <div className="rounded-lg border overflow-hidden">
      <table className="w-full text-sm">
        <thead>
          <tr className="bg-muted/50 border-b">
            <th className="text-left p-3 font-medium">Store</th>
            <th className="text-right p-3 font-medium">Regular</th>
            <th className="text-right p-3 font-medium">Current</th>
            <th className="text-right p-3 font-medium">Discount</th>
          </tr>
        </thead>
        <tbody>
          {STORES.map((store, i) => {
            const p = priceMap[store.code]
            return (
              <tr key={store.code} className={i % 2 === 0 ? '' : 'bg-muted/20'}>
                <td className="p-3 font-medium">{store.name}</td>
                {p ? (
                  <>
                    <td className="p-3 text-right text-muted-foreground">
                      {p.regularPrice ? `$${p.regularPrice.toFixed(2)}` : '—'}
                    </td>
                    <td className="p-3 text-right font-semibold text-primary">${p.currentPrice.toFixed(2)}</td>
                    <td className="p-3 text-right">
                      <DiscountCell percent={p.discountPercent} />
                    </td>
                  </>
                ) : (
                  <td colSpan={3} className="p-3 text-center text-muted-foreground italic">
                    Not available
                  </td>
                )}
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
