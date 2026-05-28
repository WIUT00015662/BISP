import { Navigate } from 'react-router-dom'
import { getToken, getUser } from '@/lib/auth'

export function ProtectedRoute({ children, admin = false, subscription = false }) {
  const token = getToken()
  const user = getUser()

  if (!token || !user) return <Navigate to="/auth/login" replace />
  if (admin && user.role !== 'Admin') return <Navigate to="/" replace />

  return children
}
