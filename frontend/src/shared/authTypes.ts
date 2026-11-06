export type AuthUser = {
  id: string
  tenantId: string
  companyName: string
  fullName: string
  email: string
  role: string
}

export type AuthSession = {
  accessToken: string
  accessTokenExpiresAt: string
  user: AuthUser
}

export type AuthFields = {
  companyName: string
  fullName: string
  email: string
  password: string
}

export type AuthMode = 'login' | 'register'
export type ApiStatus = 'checking' | 'online' | 'offline'
