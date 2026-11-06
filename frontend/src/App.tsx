import { useCallback, useEffect, useState } from 'react'
import { AuthView } from './features/auth/AuthView'
import { useAuth } from './features/auth/useAuth'
import { CustomersPage } from './features/customers/CustomersPage'
import { DashboardPage } from './features/dashboard/DashboardPage'
import type { DashboardSummary } from './features/dashboard/types'
import { AuthenticatedLayout } from './layout/AuthenticatedLayout'
import { SessionExpiredError } from './shared/api'
import type { ApiStatus } from './shared/authTypes'
import { OperationsPage } from './features/operations/OperationsPage'
import './App.css'

type OperationPage = 'Instalações' | 'Chamados' | 'Ordens de serviço' | 'Manutenção'

function isOperationPage(page: string): page is OperationPage {
  return ['Instalações', 'Chamados', 'Ordens de serviço', 'Manutenção'].includes(page)
}

function App() {
  const [activePage, setActivePage] = useState('Visão geral')
  const [apiStatus, setApiStatus] = useState<ApiStatus>('checking')
  const [dashboard, setDashboard] = useState<DashboardSummary | null>(null)
  const [dashboardLoading, setDashboardLoading] = useState(true)
  const [dashboardError, setDashboardError] = useState('')
  const [dataVersion, setDataVersion] = useState(0)
  const auth = useAuth(() => setDashboardLoading(true))
  const { session } = auth
  const authorizedFetch = auth.request
  const expireSession = auth.expireSession

  const handleDataChanged = useCallback(() => {
    setDataVersion((version) => version + 1)
    setDashboardLoading(true)
  }, [])

  function navigate(page: string) {
    setActivePage(page)
    if (page === 'Visão geral') {
      setDashboardLoading(true)
      setDashboardError('')
    }
  }

  useEffect(() => {
    if (!session?.accessToken || activePage !== 'Visão geral') {
      return
    }

    const controller = new AbortController()
    authorizedFetch('/api/dashboard/summary', { signal: controller.signal })
      .then(async (response) => {
        if (!response.ok) {
          throw new Error(`Não foi possível carregar o painel (HTTP ${response.status}).`)
        }
        setDashboard((await response.json()) as DashboardSummary)
        setDashboardError('')
      })
      .catch((cause: unknown) => {
        if (cause instanceof DOMException && cause.name === 'AbortError') {
          return
        }
        if (cause instanceof SessionExpiredError) {
          expireSession()
        }
        setDashboardError(cause instanceof Error ? cause.message : 'Não foi possível carregar o painel.')
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setDashboardLoading(false)
        }
      })

    return () => controller.abort()
  }, [activePage, authorizedFetch, dataVersion, expireSession, session?.accessToken])

  useEffect(() => {
    const controller = new AbortController()
    fetch('/api/health/live', { signal: controller.signal })
      .then((response) => {
        if (!response.ok) {
          throw new Error(`Health endpoint returned ${response.status}`)
        }
        setApiStatus('online')
      })
      .catch((cause: unknown) => {
        if (cause instanceof DOMException && cause.name === 'AbortError') {
          return
        }
        setApiStatus('offline')
      })

    return () => controller.abort()
  }, [])

  if (auth.loading) {
    return (
      <main className="auth-loading" role="status">
        <span className="brand-mark" aria-hidden="true">S</span>
        <span>Verificando sua sessão...</span>
      </main>
    )
  }

  if (!session) {
    return (
      <AuthView
        apiStatus={apiStatus}
        error={auth.error}
        mode={auth.mode}
        onModeChange={(mode) => {
          auth.setMode(mode)
          auth.setError('')
        }}
        onSubmit={auth.submit}
        submitting={auth.submitting}
      />
    )
  }

  const dashboardPage = activePage === 'Visão geral'
  const customersPage = activePage === 'Clientes'
  const operationPage = isOperationPage(activePage)
  const description = dashboardPage
    ? 'Acompanhe o pós-venda e a operação da sua empresa.'
    : customersPage
      ? 'Cadastre e mantenha os dados dos clientes da sua empresa.'
      : operationPage
        ? 'Cadastre e acompanhe os registros da operação da sua empresa.'
        : `A área de ${activePage.toLowerCase()} está sendo preparada.`
  const eyebrow = dashboardPage
    ? 'PAINEL DE OPERAÇÃO'
    : customersPage
      ? 'GESTÃO DE CLIENTES'
      : 'GESTÃO DA OPERAÇÃO'

  return (
    <AuthenticatedLayout
      activePage={activePage}
      apiStatus={apiStatus}
      description={description}
      eyebrow={eyebrow}
      onLogout={() => void auth.logout()}
      onNavigate={navigate}
      session={session}
      showDateFilter={dashboardPage}
    >
      {dashboardPage ? (
        <DashboardPage dashboard={dashboard} loading={dashboardLoading} error={dashboardError} />
      ) : customersPage ? (
        <CustomersPage
          request={authorizedFetch}
          onSessionExpired={expireSession}
          onDataChanged={handleDataChanged}
          refreshVersion={dataVersion}
          role={session.user.role}
        />
      ) : operationPage ? (
        <OperationsPage
          page={activePage}
          request={authorizedFetch}
          onDataChanged={handleDataChanged}
          refreshVersion={dataVersion}
          role={session.user.role}
        />
      ) : (
        <section className="panel module-placeholder">
          <div className="empty-icon" aria-hidden="true">
            <svg viewBox="0 0 48 48">
              <path d="M10 13h28M10 24h28M10 35h18" />
            </svg>
          </div>
          <h2>Módulo em desenvolvimento</h2>
          <p>Esta tela será habilitada quando o fluxo estiver implementado e validado.</p>
          <button className="back-button" onClick={() => navigate('Visão geral')} type="button">
            Voltar para visão geral
          </button>
        </section>
      )}
    </AuthenticatedLayout>
  )
}

export default App
