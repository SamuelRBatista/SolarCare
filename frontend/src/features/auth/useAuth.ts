import { useCallback, useEffect, useState } from 'react'
import { fetchWithSession, getInitialSession, SessionExpiredError } from '../../shared/api'
import type { AuthFields, AuthMode, AuthSession } from '../../shared/authTypes'

export function useAuth(onAuthenticated: () => void) {
  const [session, setSession] = useState<AuthSession | null>(null)
  const [loading, setLoading] = useState(true)
  const [mode, setMode] = useState<AuthMode>('login')
  const [error, setError] = useState('')
  const [submitting, setSubmitting] = useState(false)

  const request = useCallback(async (path: string, init?: RequestInit) => {
    if (!session) {
      throw new SessionExpiredError()
    }
    return fetchWithSession(path, init, session, setSession)
  }, [session])

  const expireSession = useCallback(() => {
    setSession(null)
    setMode('login')
  }, [])

  useEffect(() => {
    let active = true
    getInitialSession()
      .then((currentSession) => {
        if (active && currentSession) {
          setSession(currentSession)
        }
      })
      .catch((cause: unknown) => {
        if (active) {
          setError(cause instanceof Error ? cause.message : 'Não foi possível verificar a sessão.')
        }
      })
      .finally(() => {
        if (active) {
          setLoading(false)
        }
      })

    return () => {
      active = false
    }
  }, [])

  async function submit(fields: AuthFields) {
    setSubmitting(true)
    setError('')

    try {
      const response = await fetch(`/api/auth/${mode === 'register' ? 'register' : 'login'}`, {
        method: 'POST',
        credentials: 'same-origin',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(
          mode === 'register'
            ? fields
            : { email: fields.email, password: fields.password },
        ),
      })

      if (!response.ok) {
        const message = response.status === 401
          ? 'E-mail ou senha inválidos.'
          : response.status === 409
            ? 'Não foi possível criar uma conta com os dados informados.'
            : response.status === 429
              ? 'Muitas tentativas. Aguarde um minuto e tente novamente.'
              : response.status === 400
                ? 'Confira os dados informados. A senha deve ter pelo menos 12 caracteres.'
                : `Não foi possível concluir a solicitação (HTTP ${response.status}).`
        throw new Error(message)
      }

      setSession((await response.json()) as AuthSession)
      onAuthenticated()
    } catch (cause: unknown) {
      setError(cause instanceof Error ? cause.message : 'Não foi possível concluir a solicitação.')
    } finally {
      setSubmitting(false)
    }
  }

  async function logout() {
    setError('')
    try {
      const response = await fetch('/api/auth/logout', {
        method: 'POST',
        credentials: 'same-origin',
      })
      if (!response.ok) {
        throw new Error(`Não foi possível encerrar a sessão (HTTP ${response.status}).`)
      }
      setSession(null)
      setMode('login')
    } catch (cause: unknown) {
      setError(cause instanceof Error ? cause.message : 'Não foi possível encerrar a sessão.')
    }
  }

  return {
    session,
    loading,
    mode,
    setMode,
    error,
    setError,
    submitting,
    request,
    submit,
    logout,
    expireSession,
  }
}
