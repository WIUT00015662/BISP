export function getToken() {
  return localStorage.getItem('token')
}

export function setToken(token) {
  localStorage.setItem('token', token)
}

export function clearToken() {
  localStorage.removeItem('token')
}

export function getUser() {
  const token = getToken()
  if (!token) return null
  try {
    const payload = JSON.parse(atob(token.split('.')[1]))
    return {
      id: payload.sub,
      email: payload.email,
      role: payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || payload.role || 'User',
    }
  } catch {
    return null
  }
}

export function isAdmin() {
  return getUser()?.role === 'Admin'
}
