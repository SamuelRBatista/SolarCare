import type { DashboardSummary } from './types'
import { FeedbackMessage } from '../../components/FeedbackMessage'

const metrics: { label: string; icon: string; key: keyof DashboardSummary; note: string }[] = [
  { label: 'Clientes', icon: '♧', key: 'customerCount', note: 'Clientes ativos' },
  { label: 'Instalações', icon: '▦', key: 'installationCount', note: 'Instalações ativas' },
  { label: 'Chamados abertos', icon: '◷', key: 'openTicketCount', note: 'Aguardando atendimento' },
  { label: 'Ordens pendentes', icon: '☷', key: 'pendingServiceOrderCount', note: 'Em execução ou agendadas' },
]

function formatCount(count: number, singular: string, plural: string): string {
  return `${count.toLocaleString('pt-BR')} ${count === 1 ? singular : plural}`
}

export function DashboardPage({
  dashboard,
  loading,
  error,
}: {
  dashboard: DashboardSummary | null
  loading: boolean
  error: string
}) {
  return (
    <>
      {error && <FeedbackMessage>{error}</FeedbackMessage>}
      <section className="metrics-grid" aria-label="Indicadores">
        {metrics.map((metric) => (
          <article className="metric-card" key={metric.label}>
            <div className="metric-topline">
              <span className="metric-label">{metric.label}</span>
              <span className="metric-icon" aria-hidden="true">{metric.icon}</span>
            </div>
            <div className="metric-value" aria-label={loading ? 'Carregando indicador' : metric.label}>
              {loading ? '…' : dashboard ? dashboard[metric.key].toLocaleString('pt-BR') : '—'}
            </div>
            <div className="metric-note">{metric.note}</div>
          </article>
        ))}
      </section>

      <section className="dashboard-grid">
        <article className="panel activity-panel">
          <div className="panel-heading">
            <div>
              <h2>Atividade recente</h2>
              <p>Últimas atualizações da operação</p>
            </div>
            <button className="text-button" type="button" disabled>
              Ver tudo <span aria-hidden="true">→</span>
            </button>
          </div>
          <div className="empty-state">
            <div className="empty-icon" aria-hidden="true">
              <svg viewBox="0 0 48 48">
                <path d="M24 8a16 16 0 1 0 16 16M24 16v9l6 4" />
                <path d="M31 8h9v9" />
              </svg>
            </div>
            <strong>Nenhuma atividade por aqui</strong>
            <span>Quando sua equipe registrar atualizações, elas aparecerão neste espaço.</span>
          </div>
        </article>

        <article className="panel attention-panel">
          <div className="panel-heading">
            <div>
              <h2>Precisa de atenção</h2>
              <p>Pendências que merecem acompanhamento</p>
            </div>
          </div>
          <div className="attention-empty">
            <span className="attention-check" aria-hidden="true">✓</span>
            <div>
              <strong>
                {loading
                  ? 'Carregando pendências...'
                  : dashboard
                    ? formatCount(
                      dashboard.overdueMaintenanceCount,
                      'manutenção atrasada',
                      'manutenções atrasadas',
                    )
                    : 'Dados indisponíveis'}
              </strong>
              <span>
                {loading
                  ? ' '
                  : dashboard
                    ? `${formatCount(dashboard.upcomingMaintenanceCount, 'próxima', 'próximas')}; ${formatCount(
                      dashboard.warrantiesExpiringSoon,
                      'garantia vence em até 90 dias',
                      'garantias vencem em até 90 dias',
                    )}.`
                    : 'Os dados não puderam ser carregados.'}
              </span>
            </div>
          </div>
          {error && <div className="attention-footnote">Não foi possível atualizar os indicadores.</div>}
        </article>
      </section>

      <section className="getting-started">
        <div className="getting-started-icon" aria-hidden="true">✦</div>
        <div>
          <strong>Comece pelo cadastro da operação</strong>
          <p>Cadastre clientes e instalações para acompanhar atendimentos, ordens de serviço e manutenções.</p>
        </div>
        <span className="getting-started-tag">PRÓXIMAS ETAPAS</span>
      </section>
    </>
  )
}
