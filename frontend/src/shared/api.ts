import type { AuthSession } from './authTypes'

export class SessionExpiredError extends Error {
  constructor() {
    super('Sua sessão expirou. Entre novamente para continuar.')
    this.name = 'SessionExpiredError'
  }
}

export async function getResponseError(response: Response, fallback: string): Promise<Error> {
  let detail = ''
  if (response.headers.get('content-type')?.includes('application/json')) {
    try {
      const problem = (await response.json()) as {
        detail?: string
        title?: string
        errors?: Record<string, unknown>
      }
      detail = problem.detail || ''
      if (!detail && problem.errors) {
        for (const value of Object.values(problem.errors)) {
          if (typeof value === 'string') {
            detail = value
            break
          }
          if (Array.isArray(value)) {
            const firstMessage = value.find((message): message is string => typeof message === 'string')
            if (firstMessage) {
              detail = firstMessage
              break
            }
          }
        }
      }
      if (!detail) detail = problem.title || ''
    } catch {
      detail = ''
    }
  }

  if (!detail && response.status === 409) {
    detail = 'Já existe um cliente com este CPF/CNPJ nesta empresa.'
  } else if (!detail && response.status === 400) {
    detail = 'Confira os dados informados e tente novamente.'
  }
  return new Error(detail || `${fallback} (HTTP ${response.status}).`)
}

export async function fetchWithSession(
  path: string,
  init: RequestInit | undefined,
  session: AuthSession,
  onSessionRefresh: (session: AuthSession) => void,
): Promise<Response> {
  const send = (accessToken: string) => {
    const headers = new Headers(init?.headers)
    headers.set('Authorization', `Bearer ${accessToken}`)
    return fetch(path, { ...init, headers, credentials: 'same-origin' })
  }

  let response = await send(session.accessToken)
  if (response.status !== 401) {
    return response
  }

  const refreshResponse = await fetch('/api/auth/refresh', {
    method: 'POST',
    credentials: 'same-origin',
  })
  if (refreshResponse.status === 401) {
    throw new SessionExpiredError()
  }
  if (!refreshResponse.ok) {
    throw new Error(`Não foi possível renovar a sessão (HTTP ${refreshResponse.status}).`)
  }

  const refreshedSession = (await refreshResponse.json()) as AuthSession
  onSessionRefresh(refreshedSession)
  response = await send(refreshedSession.accessToken)
  if (response.status === 401) {
    throw new SessionExpiredError()
  }
  return response
}

let initialSessionCheck: Promise<AuthSession | null> | null = null

export function getInitialSession(): Promise<AuthSession | null> {
  if (!initialSessionCheck) {
    initialSessionCheck = fetch('/api/auth/refresh', {
      method: 'POST',
      credentials: 'same-origin',
    })
      .then(async (response) => {
        if (response.status === 401) {
          return null
        }
        if (!response.ok) {
          throw new Error(`Não foi possível verificar a sessão (HTTP ${response.status}).`)
        }
        return (await response.json()) as AuthSession
      })
      .catch((error: unknown) => {
        initialSessionCheck = null
        throw error
      })
  }
  return initialSessionCheck
}
