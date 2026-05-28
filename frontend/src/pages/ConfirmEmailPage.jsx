import { useEffect, useState } from 'react'
import { useSearchParams, Link } from 'react-router-dom'
import { CheckCircle, XCircle, Loader2 } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import api from '@/lib/api'

export default function ConfirmEmailPage() {
  const [params] = useSearchParams()
  const [status, setStatus] = useState('loading')

  useEffect(() => {
    const userId = params.get('userId')
    const token = params.get('token')
    if (!userId || !token) { setStatus('error'); return }

    api.get('/api/auth/confirm-email', { params: { userId, token } })
      .then(() => setStatus('success'))
      .catch(() => setStatus('error'))
  }, [params])

  return (
    <main className="container flex items-center justify-center min-h-[80vh]">
      <Card className="w-full max-w-md text-center">
        <CardContent className="pt-8 pb-8 space-y-4">
          {status === 'loading' && (
            <>
              <Loader2 className="h-16 w-16 text-primary mx-auto animate-spin" />
              <h2 className="text-2xl font-bold">Confirming your email…</h2>
            </>
          )}
          {status === 'success' && (
            <>
              <CheckCircle className="h-16 w-16 text-green-500 mx-auto" />
              <h2 className="text-2xl font-bold">Email confirmed!</h2>
              <p className="text-muted-foreground">Your account is ready. You can now sign in.</p>
              <Button asChild><Link to="/auth/login">Sign in</Link></Button>
            </>
          )}
          {status === 'error' && (
            <>
              <XCircle className="h-16 w-16 text-destructive mx-auto" />
              <h2 className="text-2xl font-bold">Confirmation failed</h2>
              <p className="text-muted-foreground">The link may have expired or is invalid.</p>
              <Button variant="outline" asChild><Link to="/auth/login">Back to Login</Link></Button>
            </>
          )}
        </CardContent>
      </Card>
    </main>
  )
}
