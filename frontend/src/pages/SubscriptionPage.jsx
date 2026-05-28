import { useState, useEffect } from 'react'
import { CheckCircle, Star, Zap, Bell } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import api from '@/lib/api'

const FEATURES = [
  { icon: Bell, text: 'Price drop email alerts' },
  { icon: Star, text: 'Unlimited wishlist items' },
  { icon: Zap, text: 'Instant discount notifications' },
]

export default function SubscriptionPage() {
  const [status, setStatus] = useState(null)
  const [loading, setLoading] = useState(true)
  const [checkingOut, setCheckingOut] = useState(false)
  const [message, setMessage] = useState(null)

  useEffect(() => {
    const params = new URLSearchParams(window.location.search)
    if (params.get('success')) setMessage({ type: 'success', text: 'Subscription activated! Welcome to BISP Premium.' })
    if (params.get('canceled')) setMessage({ type: 'info', text: 'Checkout canceled. No charges were made.' })

    api.get('/api/subscriptions/status')
      .then((r) => setStatus(r.data))
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [])

  async function handleSubscribe() {
    setCheckingOut(true)
    try {
      const res = await api.post('/api/subscriptions/checkout')
      window.location.href = res.data.url
    } catch {
      setMessage({ type: 'error', text: 'Failed to start checkout. Please try again.' })
      setCheckingOut(false)
    }
  }

  return (
    <main className="container py-8 max-w-lg">
      <h1 className="text-3xl font-bold tracking-tight mb-2">BISP Premium</h1>
      <p className="text-muted-foreground mb-6">Unlock wishlist and price alerts for any game you track.</p>

      {message && (
        <div className={`p-3 rounded-md mb-4 text-sm ${
          message.type === 'success' ? 'bg-green-500/10 text-green-600 dark:text-green-400' :
          message.type === 'error' ? 'bg-destructive/10 text-destructive' :
          'bg-muted text-muted-foreground'
        }`}>
          {message.text}
        </div>
      )}

      {loading ? (
        <div className="animate-pulse h-48 bg-muted rounded-lg" />
      ) : status?.hasActiveSubscription ? (
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <CardTitle>Active Subscription</CardTitle>
              <Badge variant="success">Active</Badge>
            </div>
          </CardHeader>
          <CardContent className="space-y-3">
            <p className="text-muted-foreground">
              Your subscription is active.
              {status.currentPeriodEndUtc && (
                <> Renews on {new Date(status.currentPeriodEndUtc).toLocaleDateString()}.</>
              )}
            </p>
            <ul className="space-y-2">
              {FEATURES.map(({ icon: Icon, text }) => (
                <li key={text} className="flex items-center gap-2 text-sm">
                  <CheckCircle className="h-4 w-4 text-green-500 shrink-0" />
                  {text}
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardContent className="pt-6 space-y-4">
            <div className="text-center">
              <p className="text-5xl font-bold">$2.99</p>
              <p className="text-muted-foreground">per month</p>
            </div>

            <ul className="space-y-2">
              {FEATURES.map(({ icon: Icon, text }) => (
                <li key={text} className="flex items-center gap-2 text-sm">
                  <Icon className="h-4 w-4 text-primary shrink-0" />
                  {text}
                </li>
              ))}
            </ul>

            <Button className="w-full" size="lg" onClick={handleSubscribe} disabled={checkingOut}>
              {checkingOut ? 'Redirecting to Stripe…' : 'Subscribe Now'}
            </Button>

            <p className="text-center text-xs text-muted-foreground">
              Secure payment via Stripe. Cancel anytime.
            </p>
          </CardContent>
        </Card>
      )}
    </main>
  )
}
