import { BrowserRouter as Router, Routes, Route } from 'react-router-dom'
import { ThemeProvider } from '@/components/ThemeProvider'
import { Navbar } from '@/components/Navbar'
import { ProtectedRoute } from '@/components/ProtectedRoute'
import HomePage from '@/pages/HomePage'
import GameDetailPage from '@/pages/GameDetailPage'
import LoginPage from '@/pages/LoginPage'
import RegisterPage from '@/pages/RegisterPage'
import ConfirmEmailPage from '@/pages/ConfirmEmailPage'
import WishlistPage from '@/pages/WishlistPage'
import SubscriptionPage from '@/pages/SubscriptionPage'
import AdminPage from '@/pages/AdminPage'

export default function App() {
  return (
    <ThemeProvider>
      <Router>
        <div className="min-h-screen bg-background flex flex-col">
          <Navbar />
          <div className="flex-1">
            <Routes>
              <Route path="/" element={<HomePage />} />
              <Route path="/games/:id" element={<GameDetailPage />} />
              <Route path="/auth/login" element={<LoginPage />} />
              <Route path="/auth/register" element={<RegisterPage />} />
              <Route path="/auth/confirm-email" element={<ConfirmEmailPage />} />
              <Route path="/wishlist" element={<ProtectedRoute><WishlistPage /></ProtectedRoute>} />
              <Route path="/subscription" element={<ProtectedRoute><SubscriptionPage /></ProtectedRoute>} />
              <Route path="/admin" element={<ProtectedRoute admin><AdminPage /></ProtectedRoute>} />
            </Routes>
          </div>
        </div>
      </Router>
    </ThemeProvider>
  )
}
