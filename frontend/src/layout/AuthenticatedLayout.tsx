import type { ReactNode } from 'react'
import type { ApiStatus, AuthSession } from '../shared/authTypes'

const navigation = [
  { label: 'Visão geral', icon: '⌂' },
  { label: 'Clientes', icon: '♧' },
  { label: 'Instalações', icon: '▦' },
  { label: 'Chamados', icon: '◷' },
  { label: 'Ordens de serviço', icon: '☷' },
  { label: 'Manutenção', icon: '⌁' },
]

type AuthenticatedLayoutProps = {
  session: AuthSession
  activePage: string
  apiStatus: ApiStatus
  eyebrow: string
  description: string
  showDateFilter: boolean
  onNavigate: (page: string) => void
  onLogout: () => void
  children: ReactNode
}

export function AuthenticatedLayout({
  session,
  activePage,
  apiStatus,
  eyebrow,
  description,
  showDateFilter,
  onNavigate,
  onLogout,
  children,
}: AuthenticatedLayoutProps) {
  return (
    <div className="app-shell">
      <aside className="sidebar">
        <a className="brand" href="/" aria-label="SolarCare, inicio">
          <span className="brand-mark" aria-hidden="true">S</span>
          <span className="brand-name">solarcare<span>.</span></span>
        </a>

        <div className="workspace-label">ESPAÇO DE TRABALHO</div>
        <button className="workspace-switcher" type="button" aria-label="Empresa atual" disabled>
          <span className="workspace-avatar">SC</span>
          <span className="workspace-copy">
            <strong>{session.user.companyName}</strong>
            <small>Espaço de trabalho</small>
          </span>
          <span className="chevron" aria-hidden="true">⌄</span>
        </button>

        <nav className="navigation" aria-label="Navegação principal">
          <span className="nav-section-label">OPERAÇÃO</span>
          {navigation.map((item) => (
            <button
              className={`nav-item${activePage === item.label ? ' active' : ''}`}
              key={item.label}
              onClick={() => onNavigate(item.label)}
              type="button"
              aria-current={activePage === item.label ? 'page' : undefined}
            >
              <span className="nav-icon" aria-hidden="true">{item.icon}</span>
              <span>{item.label}</span>
            </button>
          ))}
        </nav>

        <div className="sidebar-bottom">
          <div className="help-card">
            <span className="help-icon" aria-hidden="true">?</span>
            <div>
              <strong>Precisa de ajuda?</strong>
              <span>Fale com nosso time</span>
            </div>
            <span aria-hidden="true">↗</span>
          </div>
          <div className="profile">
            <span className="profile-avatar">SC</span>
            <span className="profile-copy">
              <strong>{session.user.fullName}</strong>
              <small>{session.user.role}</small>
            </span>
            <button className="profile-menu" onClick={onLogout} type="button" aria-label="Sair da conta">
              Sair
            </button>
          </div>
        </div>
      </aside>

      <main className="main-content">
        <header className="topbar">
          <div className="breadcrumbs">
            <span>SolarCare</span>
            <span className="breadcrumb-divider">/</span>
            <strong>{activePage}</strong>
          </div>
          <div className="topbar-actions">
            <div className={`api-indicator ${apiStatus}`} role="status">
              <span className="status-dot" />
              {apiStatus === 'checking' && 'Conectando a API'}
              {apiStatus === 'online' && 'API conectada'}
              {apiStatus === 'offline' && 'API indisponível'}
            </div>
            <button className="icon-button notification-button" type="button" aria-label="Notificações" disabled>
              <svg viewBox="0 0 24 24" aria-hidden="true">
                <path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9M10 21h4" />
              </svg>
              <span className="notification-dot" />
            </button>
            <span className="topbar-avatar" aria-label={session.user.fullName}>
              {session.user.fullName.slice(0, 1).toUpperCase()}
            </span>
            <button className="topbar-logout" onClick={onLogout} type="button">Sair</button>
          </div>
        </header>

        <div className="page-content">
          <div className="page-heading">
            <div>
              <div className="eyebrow">{eyebrow}</div>
              <h1>{activePage}</h1>
              <p>{description}</p>
            </div>
            {showDateFilter && (
              <button className="date-filter" type="button" disabled>
                <svg viewBox="0 0 24 24" aria-hidden="true">
                  <rect x="3.5" y="5" width="17" height="16" rx="2" />
                  <path d="M16 3v4M8 3v4M3.5 10h17" />
                </svg>
                <span>Período atual</span>
                <span className="chevron" aria-hidden="true">⌄</span>
              </button>
            )}
          </div>
          {children}
          <footer className="page-footer">
            <span>SolarCare <span className="footer-separator">·</span> Gestão de pós-venda solar</span>
            <span className="footer-version">AMBIENTE DE DESENVOLVIMENTO</span>
          </footer>
        </div>
      </main>
    </div>
  )
}
