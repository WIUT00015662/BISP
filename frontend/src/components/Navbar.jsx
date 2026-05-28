import { Link, useNavigate } from 'react-router-dom'
import { Gamepad2, Heart, Shield, LogOut, LogIn, UserPlus } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { ThemeToggle } from '@/components/ThemeToggle'
import { getUser, clearToken } from '@/lib/auth'

export function Navbar() {
  const navigate = useNavigate()
  const user = getUser()

  function handleLogout() {
    clearToken()
    navigate('/auth/login')
  }

  return (
    <header className="sticky top-0 z-50 w-full border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60">
      <div className="container flex h-16 items-center justify-between">
        <Link to="/" className="flex items-center gap-2 font-bold text-xl text-primary">
          <Gamepad2 className="h-6 w-6" />
          BISP
        </Link>

        <nav className="flex items-center gap-2">
          {user ? (
            <>
              <Button variant="ghost" size="sm" asChild>
                <Link to="/wishlist" className="flex items-center gap-1">
                  <Heart className="h-4 w-4" />
                  Wishlist
                </Link>
              </Button>

              {user.role === 'Admin' && (
                <Button variant="ghost" size="sm" asChild>
                  <Link to="/admin" className="flex items-center gap-1">
                    <Shield className="h-4 w-4" />
                    Admin
                  </Link>
                </Button>
              )}

              <span className="text-sm text-muted-foreground hidden sm:block">{user.email}</span>

              <Button variant="ghost" size="sm" onClick={handleLogout} className="flex items-center gap-1">
                <LogOut className="h-4 w-4" />
                Logout
              </Button>
            </>
          ) : (
            <>
              <Button variant="ghost" size="sm" asChild>
                <Link to="/auth/login" className="flex items-center gap-1">
                  <LogIn className="h-4 w-4" />
                  Login
                </Link>
              </Button>
              <Button size="sm" asChild>
                <Link to="/auth/register" className="flex items-center gap-1">
                  <UserPlus className="h-4 w-4" />
                  Register
                </Link>
              </Button>
            </>
          )}

          <ThemeToggle />
        </nav>
      </div>
    </header>
  )
}
