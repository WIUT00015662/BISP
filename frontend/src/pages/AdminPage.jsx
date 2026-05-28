import { useState } from 'react'
import { RefreshCw, CheckCircle, AlertCircle } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import api from '@/lib/api'

export default function AdminPage() {
  const [status, setStatus] = useState(null)
  const [running, setRunning] = useState(false)
  const [lastRun, setLastRun] = useState(null)

  async function handleAggregate() {
    setRunning(true)
    setStatus(null)
    try {
      await api.post('/api/admin/aggregate')
      setStatus('success')
      setLastRun(new Date())
    } catch {
      setStatus('error')
    } finally {
      setRunning(false)
    }
  }

  return (
    <main className="container py-8 max-w-lg">
      <h1 className="text-3xl font-bold tracking-tight mb-2">Admin Panel</h1>
      <p className="text-muted-foreground mb-6">Manage data aggregation and system settings.</p>

      <Card>
        <CardHeader>
          <CardTitle>Price Aggregation</CardTitle>
          <CardDescription>
            Queries IGDB for top games, then fetches current prices from Steam, GOG, and Epic Games.
            Runs automatically every 10 hours — use this to trigger a manual run.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {status === 'success' && (
            <div className="flex items-center gap-2 p-3 rounded-md bg-green-500/10 text-green-600 dark:text-green-400 text-sm">
              <CheckCircle className="h-4 w-4 shrink-0" />
              Aggregation started. Check server logs for progress.
            </div>
          )}
          {status === 'error' && (
            <div className="flex items-center gap-2 p-3 rounded-md bg-destructive/10 text-destructive text-sm">
              <AlertCircle className="h-4 w-4 shrink-0" />
              Failed to start aggregation. Are you logged in as admin?
            </div>
          )}

          <Button onClick={handleAggregate} disabled={running} className="w-full">
            <RefreshCw className={`h-4 w-4 mr-2 ${running ? 'animate-spin' : ''}`} />
            {running ? 'Starting…' : 'Run Aggregation'}
          </Button>

          {lastRun && (
            <p className="text-center text-xs text-muted-foreground">
              Last triggered at {lastRun.toLocaleTimeString()}
            </p>
          )}
        </CardContent>
      </Card>
    </main>
  )
}
