import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Gamepad2, MailCheck } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import api from '@/lib/api'

export default function RegisterPage() {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [error, setError] = useState(null)
  const [success, setSuccess] = useState(false)
  const [loading, setLoading] = useState(false)

  async function handleSubmit(e) {
    e.preventDefault()
    setError(null)
    if (password !== confirm) { setError('Passwords do not match.'); return }
    setLoading(true)
    try {
      await api.post('/api/auth/register', { email, password, confirmPassword: confirm })
      setSuccess(true)
    } catch (err) {
      const errors = err.response?.data?.errors
      setError(errors ? errors.join(' ') : err.response?.data?.error || 'Registration failed.')
    } finally {
      setLoading(false)
    }
  }

  if (success) {
    return (
      <main className="container flex items-center justify-center min-h-[80vh]">
        <Card className="w-full max-w-md text-center">
          <CardContent className="pt-8 pb-8 space-y-4">
            <MailCheck className="h-16 w-16 text-primary mx-auto" />
            <h2 className="text-2xl font-bold">Check your email</h2>
            <p className="text-muted-foreground">
              We sent a confirmation link to <strong>{email}</strong>.<br />
              Click the link to activate your account.
            </p>
            <Button variant="outline" asChild>
              <Link to="/auth/login">Back to Login</Link>
            </Button>
          </CardContent>
        </Card>
      </main>
    )
  }

  return (
    <main className="container flex items-center justify-center min-h-[80vh]">
      <Card className="w-full max-w-md">
        <CardHeader className="text-center">
          <div className="flex justify-center mb-2">
            <Gamepad2 className="h-10 w-10 text-primary" />
          </div>
          <CardTitle className="text-2xl">Create account</CardTitle>
          <CardDescription>Join BISP to track game prices</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-4">
            {error && (
              <div className="p-3 rounded-md bg-destructive/10 text-destructive text-sm">{error}</div>
            )}

            <div className="space-y-1.5">
              <Label htmlFor="email">Email</Label>
              <Input id="email" type="email" placeholder="you@example.com" value={email} onChange={(e) => setEmail(e.target.value)} required />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="password">Password</Label>
              <Input id="password" type="password" placeholder="Min. 8 characters" value={password} onChange={(e) => setPassword(e.target.value)} required minLength={8} />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="confirm">Confirm Password</Label>
              <Input id="confirm" type="password" placeholder="••••••••" value={confirm} onChange={(e) => setConfirm(e.target.value)} required />
            </div>

            <Button type="submit" className="w-full" disabled={loading}>
              {loading ? 'Creating account…' : 'Create account'}
            </Button>
          </form>

          <p className="text-center text-sm text-muted-foreground mt-4">
            Already have an account?{' '}
            <Link to="/auth/login" className="text-primary hover:underline font-medium">Sign in</Link>
          </p>
        </CardContent>
      </Card>
    </main>
  )
}
