import { useState } from 'react'
import { FeedbackMessage } from '../../components/FeedbackMessage'
import {
  readJson,
  useOperations,
  type ApiRequest,
  type Customer,
  type Installation,
  type MaintenancePlan,
  type OperationPage,
  type RelatedData,
  type ServiceOrder,
  type Ticket,
} from './useOperations'

const ticketStatuses = [
  ['Open', 'Aberto'],
  ['InProgress', 'Em andamento'],
  ['WaitingCustomer', 'Aguardando cliente'],
  ['WaitingParts', 'Aguardando peças'],
  ['Resolved', 'Resolvido'],
  ['Closed', 'Fechado'],
  ['Cancelled', 'Cancelado'],
] as const

const orderStatuses = [
  ['Pending', 'Pendente'],
  ['Scheduled', 'Agendada'],
  ['InProgress', 'Em andamento'],
  ['Completed', 'Concluída'],
  ['Cancelled', 'Cancelada'],
] as const

function formatDate(value: string | null | undefined): string {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString('pt-BR')
}

function today(): string {
  return new Date().toISOString().slice(0, 10)
}

export function OperationsPage({
  page,
  request,
  onDataChanged,
  refreshVersion,
  role,
}: {
  page: OperationPage
  request: ApiRequest
  onDataChanged: () => void
  refreshVersion: number
  role: string
}) {
  const {
    rows,
    related,
    loadState,
    error,
    formOpen,
    saving,
    deactivatingId,
    editingId,
    formError,
    statusChanges,
    setStatusChanges,
    history,
    setHistory,
    actionId,
    isInstallations,
    isTickets,
    isOrders,
    isMaintenance,
    loadKey,
    loading,
    openCreateForm,
    openEditForm,
    closeForm,
    submit,
    deactivate,
    updateStatus,
    showHistory,
  } = useOperations({ page, request, onDataChanged, refreshVersion })

  const title = page === 'Instalações'
    ? 'Instalações'
    : page === 'Chamados'
      ? 'Chamados'
      : page === 'Ordens de serviço'
        ? 'Ordens de serviço'
        : 'Manutenção'
  const description = isInstallations
    ? 'Cadastre e acompanhe os sistemas solares de cada cliente.'
    : isTickets
      ? 'Registre solicitações e acompanhe cada chamado até a conclusão.'
      : isOrders
        ? 'Organize visitas técnicas, responsáveis, materiais e conclusão do serviço.'
        : 'Configure planos recorrentes e registre as manutenções executadas.'
  const createLabel = isInstallations
    ? 'Nova instalação'
    : isTickets
      ? 'Novo chamado'
      : isOrders
        ? 'Nova ordem de serviço'
        : 'Novo plano'
  const newRecordTitle = isInstallations
    ? 'Nova instalação'
    : isTickets
      ? 'Novo chamado'
      : isOrders
        ? 'Nova ordem de serviço'
        : 'Novo plano de manutenção'

  return (
    <section className="customers-section">
      <div className="operations-heading">
        <div>
          <div className="eyebrow">GESTÃO DA OPERAÇÃO</div>
          <h2>{title}</h2>
          <p>{description}</p>
        </div>
        {(isInstallations || isTickets || isMaintenance || (isOrders && role !== 'Technician')) && (
          <button className="primary-button" onClick={openCreateForm} type="button">
            + {createLabel}
          </button>
        )}
      </div>

      {(error || (loadState.key === loadKey ? loadState.error : '')) && (
        <FeedbackMessage>{error || loadState.error}</FeedbackMessage>
      )}

      {formOpen && (
        <form
          className="panel customer-form"
          onSubmit={(event) => {
            if (isInstallations) {
              const form = new FormData(event.currentTarget)
              void submit(event, editingId ? `/api/installations/${editingId}` : '/api/installations', editingId ? 'PUT' : 'POST', {
                customerId: form.get('customerId'),
                name: form.get('name'),
                address: form.get('address') || null,
                city: form.get('city') || null,
                state: form.get('state') || null,
                installedAt: form.get('installedAt') || null,
                capacityKwp: numberOrNull(form.get('capacityKwp')),
                moduleCount: numberOrNull(form.get('moduleCount')),
                moduleManufacturer: form.get('moduleManufacturer') || null,
                moduleModel: form.get('moduleModel') || null,
                inverterManufacturer: form.get('inverterManufacturer') || null,
                inverterModel: form.get('inverterModel') || null,
                integratorName: form.get('integratorName') || null,
                technicalResponsible: form.get('technicalResponsible') || null,
                notes: form.get('notes') || null,
              })
            } else if (isTickets) {
              const form = new FormData(event.currentTarget)
              const payload = {
                customerId: form.get('customerId'),
                installationId: form.get('installationId') || null,
                equipmentId: form.get('equipmentId') || null,
                description: form.get('description'),
                category: form.get('category'),
                priority: form.get('priority'),
                assignedUserId: (rows.find((item) => (item as Ticket).id === editingId) as Ticket | undefined)?.assignedUserId ?? null,
              }
              void submit(
                event,
                editingId ? `/api/tickets/${editingId}` : '/api/tickets',
                editingId ? 'PUT' : 'POST',
                payload,
              )
            } else if (isMaintenance) {
              const form = new FormData(event.currentTarget)
              void submit(event, editingId ? `/api/maintenance-plans/${editingId}` : '/api/maintenance-plans', editingId ? 'PUT' : 'POST', {
                installationId: form.get('installationId'),
                name: form.get('name'),
                intervalDays: Number(form.get('intervalDays')),
                nextDueAt: form.get('nextDueAt'),
                notes: form.get('notes') || null,
              })
            } else {
              const form = new FormData(event.currentTarget)
              const payload = {
                technicianUserId: (rows.find((item) => (item as ServiceOrder).id === editingId) as ServiceOrder | undefined)?.technicianUserId ?? null,
                scheduledAt: form.get('scheduledAt') ? new Date(String(form.get('scheduledAt'))).toISOString() : null,
                address: form.get('address') || null,
                description: form.get('description'),
              }
              if (editingId) {
                void submit(event, `/api/service-orders/${editingId}`, 'PUT', payload)
              } else {
                void submit(event, '/api/service-orders', 'POST', {
                  ...payload,
                  ticketId: form.get('ticketId') || null,
                  installationId: form.get('installationId'),
                  checklist: [],
                  materials: [],
                })
              }
            }
          }}
        >
          <div className="customer-form-heading">
            <div>
              <h2>{editingId ? 'Editar registro' : newRecordTitle}</h2>
              <p>Preencha os dados obrigatórios para salvar.</p>
            </div>
            <button className="secondary-button" onClick={closeForm} type="button">Cancelar</button>
          </div>
          <div className="customer-form-grid">
            {isInstallations && <InstallationForm related={related} editing={rows.find((item) => (item as Installation).id === editingId) as Installation | undefined} />}
            {isTickets && <TicketForm
              related={related}
              editing={rows.find((item) => (item as Ticket).id === editingId) as Ticket | undefined}
            />}
            {isMaintenance && <MaintenanceForm related={related} editing={rows.find((item) => (item as MaintenancePlan).id === editingId) as MaintenancePlan | undefined} />}
            {isOrders && <ServiceOrderForm
              related={related}
              editing={rows.find((item) => (item as ServiceOrder).id === editingId) as ServiceOrder | undefined}
            />}
          </div>
          {formError && <FeedbackMessage>{formError}</FeedbackMessage>}
          <div className="customer-form-actions">
            <button className="primary-button" disabled={saving} type="submit">{saving ? 'Salvando...' : 'Salvar registro'}</button>
          </div>
        </form>
      )}

      {history && (
        <div className="panel operations-history">
          <div className="customer-form-heading">
            <div><h2>Histórico — {history.label}</h2></div>
            <button className="secondary-button" onClick={() => setHistory(null)} type="button">Fechar</button>
          </div>
          {history.entries.length === 0 ? <p>Nenhuma atualização registrada.</p> : (
            <ul>
              {history.entries.map((entry, index) => (
                <li key={String(entry.id ?? index)}>
                  <strong>{String(entry.action ?? 'Atualização')}</strong>
                  <span>{formatDate(String(entry.createdAt ?? entry.performedAt ?? ''))}</span>
                  <span>{String(entry.note ?? entry.newValue ?? entry.newStatus ?? '')}</span>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      <div className="panel customer-list-panel">
        {loading ? <FeedbackMessage variant="status">Carregando registros...</FeedbackMessage>
          : rows.length === 0 ? <div className="customer-list-message">
            <strong>Nenhum registro cadastrado</strong>
            <span>Use “{createLabel}” para cadastrar o primeiro item.</span>
          </div>
            : isInstallations
              ? <InstallationsTable
                rows={rows as Installation[]}
                customers={related.customers}
                role={role}
                onEdit={(row) => openEditForm(row.id)}
                onDeactivate={(row) => void deactivate(row.id, row.name)}
                deactivatingId={deactivatingId}
              />
              : isTickets
                ? <TicketsTable
                  rows={rows as Ticket[]}
                  related={related}
                  statusChanges={statusChanges}
                  setStatusChanges={setStatusChanges}
                  updateStatus={updateStatus}
                  actionId={actionId}
                  showHistory={showHistory}
                  onEdit={(row) => openEditForm(row.id)}
                />
                : isOrders
                  ? <ServiceOrdersTable
                    rows={rows as ServiceOrder[]}
                    related={related}
                    role={role}
                    request={request}
                    onDataChanged={onDataChanged}
                    updateStatus={updateStatus}
                    actionId={actionId}
                    showHistory={showHistory}
                    onEdit={(row) => openEditForm(row.id)}
                  />
                  : <MaintenanceTable
                    rows={rows as MaintenancePlan[]}
                    installations={related.installations}
                    role={role}
                    request={request}
                    onDataChanged={onDataChanged}
                    onEdit={(row) => openEditForm(row.id)}
                    onDeactivate={(row) => void deactivate(row.id, row.name)}
                    deactivatingId={deactivatingId}
                    showHistory={showHistory}
                    actionId={actionId}
                  />
        }
      </div>
    </section>
  )
}

function numberOrNull(value: FormDataEntryValue | null): number | null {
  if (typeof value !== 'string' || value.trim() === '') return null
  const number = Number(value)
  return Number.isFinite(number) ? number : null
}

function Field({
  label,
  name,
  required,
  type = 'text',
  defaultValue,
  options,
  wide,
  min,
  max,
  step,
  rows,
}: {
  label: string
  name: string
  required?: boolean
  type?: string
  defaultValue?: string | number | null
  options?: { value: string; label: string }[]
  wide?: boolean
  min?: number
  max?: number
  step?: number
  rows?: number
}) {
  const className = `customer-field${wide ? ' customer-field-wide' : ''}`
  return (
    <label className={className}>
      {label}{required ? ' *' : ''}
      {options ? (
        <select defaultValue={defaultValue ?? ''} name={name} required={required}>
          <option value="">Selecione</option>
          {options.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
        </select>
      ) : rows ? (
        <textarea defaultValue={defaultValue ?? ''} maxLength={4000} name={name} rows={rows} />
      ) : (
        <input
          defaultValue={defaultValue ?? ''}
          max={max}
          maxLength={type === 'text' ? 240 : undefined}
          min={min}
          name={name}
          required={required}
          step={step}
          type={type}
        />
      )}
    </label>
  )
}

function InstallationForm({ related, editing }: { related: RelatedData; editing?: Installation }) {
  return (
    <>
      <Field label="Cliente" name="customerId" required options={related.customers.map((item) => ({ value: item.id, label: item.name }))} defaultValue={editing?.customerId} />
      <Field label="Nome da instalação" name="name" required wide defaultValue={editing?.name} />
      <Field label="Data de instalação" name="installedAt" type="date" defaultValue={editing?.installedAt} />
      <Field label="Potência (kWp)" name="capacityKwp" type="number" min={0.001} step={0.001} defaultValue={editing?.capacityKwp} />
      <Field label="Quantidade de módulos" name="moduleCount" type="number" min={1} step={1} defaultValue={editing?.moduleCount} />
      <Field label="Fabricante dos módulos" name="moduleManufacturer" defaultValue={editing?.moduleManufacturer} />
      <Field label="Modelo dos módulos" name="moduleModel" defaultValue={editing?.moduleModel} />
      <Field label="Fabricante do inversor" name="inverterManufacturer" defaultValue={editing?.inverterManufacturer} />
      <Field label="Modelo do inversor" name="inverterModel" defaultValue={editing?.inverterModel} />
      <Field label="Endereço" name="address" wide defaultValue={editing?.address} />
      <Field label="Cidade" name="city" defaultValue={editing?.city} />
      <Field label="Estado (UF)" name="state" defaultValue={editing?.state} />
      <Field label="Integrador" name="integratorName" defaultValue={editing?.integratorName} />
      <Field label="Responsável técnico" name="technicalResponsible" defaultValue={editing?.technicalResponsible} />
      <Field label="Observações" name="notes" wide rows={3} defaultValue={editing?.notes} />
    </>
  )
}

function TicketForm({ related, editing }: { related: RelatedData; editing?: Ticket }) {
  const [customerId, setCustomerId] = useState(editing?.customerId ?? '')
  const [installationId, setInstallationId] = useState(editing?.installationId ?? '')
  const installations = related.installations.filter((item) => item.customerId === customerId)
  const equipment = related.equipment.filter((item) => item.installationId === installationId)
  return (
    <>
      <label className="customer-field">
        Cliente *
        <select
          value={customerId}
          name="customerId"
          onChange={(event) => {
            setCustomerId(event.target.value)
            setInstallationId('')
          }}
          required
        >
          <option value="">Selecione</option>
          {related.customers.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
        </select>
      </label>
      <label className="customer-field">
        Instalação
        <select
          value={installationId}
          name="installationId"
          onChange={(event) => {
            setInstallationId(event.target.value)
            setCustomerId((event.currentTarget.form?.elements.namedItem('customerId') as HTMLSelectElement | null)?.value ?? '')
          }}
        >
          <option value="">Sem instalação específica</option>
          {installations.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
        </select>
      </label>
      <Field label="Categoria" name="category" required defaultValue={editing?.category ?? 'Suporte técnico'} />
      <Field label="Prioridade" name="priority" required defaultValue={editing?.priority ?? 'Normal'} options={[
        { value: 'Low', label: 'Baixa' }, { value: 'Normal', label: 'Normal' },
        { value: 'High', label: 'Alta' }, { value: 'Urgent', label: 'Urgente' },
      ]} />
      <label className="customer-field">
        Equipamento
        <select defaultValue={editing?.equipmentId ?? ''} name="equipmentId">
          <option value="">Sem equipamento específico</option>
          {equipment.map((item) => <option key={item.id} value={item.id}>{item.manufacturer} {item.model ?? ''}</option>)}
        </select>
      </label>
      <Field label="Descrição" name="description" required wide rows={4} defaultValue={editing?.description} />
    </>
  )
}

function MaintenanceForm({ related, editing }: { related: RelatedData; editing?: MaintenancePlan }) {
  return (
    <>
      <Field label="Instalação" name="installationId" required wide defaultValue={editing?.installationId} options={related.installations.map((item) => ({ value: item.id, label: item.name }))} />
      <Field label="Nome do plano" name="name" required wide defaultValue={editing?.name} />
      <Field label="Intervalo (dias)" name="intervalDays" type="number" required min={1} max={3650} step={1} defaultValue={editing?.intervalDays ?? 180} />
      <Field label="Próxima manutenção" name="nextDueAt" type="date" required defaultValue={editing?.nextDueAt ?? today()} />
      <Field label="Observações" name="notes" wide rows={3} defaultValue={editing?.notes} />
    </>
  )
}

function ServiceOrderForm({ related, editing }: { related: RelatedData; editing?: ServiceOrder }) {
  const scheduledAt = editing?.scheduledAt
    ? new Date(new Date(editing.scheduledAt).getTime() - new Date(editing.scheduledAt).getTimezoneOffset() * 60000)
      .toISOString()
      .slice(0, 16)
    : ''
  return (
    <>
      {!editing && <>
        <Field label="Instalação" name="installationId" required wide options={related.installations.map((item) => ({ value: item.id, label: item.name }))} />
        <Field label="Chamado relacionado" name="ticketId" options={related.tickets
          .filter((item) => !['Closed', 'Cancelled'].includes(item.status))
          .map((item) => ({ value: item.id, label: `${item.protocol} — ${item.category}` }))} />
      </>}
      <Field label="Agendamento" name="scheduledAt" type="datetime-local" defaultValue={scheduledAt} />
      <Field label="Endereço de atendimento" name="address" wide defaultValue={editing?.address} />
      <Field label="Descrição do serviço" name="description" required wide rows={4} defaultValue={editing?.description} />
    </>
  )
}

function customerName(id: string, customers: Customer[]): string {
  return customers.find((item) => item.id === id)?.name ?? 'Cliente'
}

function installationName(id: string | null, installations: Installation[]): string {
  return installations.find((item) => item.id === id)?.name ?? '—'
}

function InstallationsTable({
  rows, customers, role, onEdit, onDeactivate, deactivatingId,
}: {
  rows: Installation[]
  customers: Customer[]
  role: string
  onEdit: (row: Installation) => void
  onDeactivate: (row: Installation) => void
  deactivatingId: string | null
}) {
  return (
    <div className="customer-table-scroll"><table className="customer-table">
      <thead><tr><th>Instalação</th><th>Cliente</th><th>Potência</th><th>Local</th><th>Instalada em</th><th>Ações</th></tr></thead>
      <tbody>{rows.map((row) => <tr key={row.id}>
        <td><strong>{row.name}</strong><span>{row.moduleCount ? `${row.moduleCount} módulos` : 'Quantidade de módulos não informada'}</span></td>
        <td>{customerName(row.customerId, customers)}</td>
        <td>{row.capacityKwp ? `${row.capacityKwp.toLocaleString('pt-BR')} kWp` : '—'}</td>
        <td>{[row.city, row.state].filter(Boolean).join(' / ') || '—'}</td>
        <td>{formatDate(row.installedAt)}</td>
        <td className="customer-actions">
          <button className="row-action" onClick={() => onEdit(row)} type="button">Editar</button>
          {role !== 'Attendant' && (
            <button className="row-action danger" disabled={deactivatingId === row.id} onClick={() => onDeactivate(row)} type="button">
              {deactivatingId === row.id ? 'Desativando...' : 'Desativar'}
            </button>
          )}
        </td>
      </tr>)}</tbody>
    </table></div>
  )
}

function TicketsTable({
  rows, related, statusChanges, setStatusChanges, updateStatus, actionId, showHistory, onEdit,
}: {
  rows: Ticket[]
  related: RelatedData
  statusChanges: Record<string, string>
  setStatusChanges: (value: Record<string, string>) => void
  updateStatus: (path: string, id: string, status: string) => Promise<void>
  actionId: string | null
  showHistory: (id: string, label: string, path: string) => Promise<void>
  onEdit: (row: Ticket) => void
}) {
  return (
    <div className="customer-table-scroll"><table className="customer-table">
      <thead><tr><th>Chamado</th><th>Cliente / instalação</th><th>Prioridade</th><th>Status</th><th>Aberto em</th><th>Ações</th></tr></thead>
      <tbody>{rows.map((row) => <tr key={row.id}>
        <td><strong>{row.protocol}</strong><span>{row.category}</span><span>{row.description}</span></td>
        <td><strong>{customerName(row.customerId, related.customers)}</strong><span>{installationName(row.installationId, related.installations)}</span></td>
        <td>{row.priority}</td>
        <td>
          <select
            aria-label={`Status do chamado ${row.protocol}`}
            onChange={(event) => setStatusChanges({ ...statusChanges, [row.id]: event.target.value })}
            value={statusChanges[row.id] ?? row.status}
          >
            {ticketStatuses.map(([value, label]) => <option key={value} value={value}>{label}</option>)}
          </select>
          {statusChanges[row.id] && statusChanges[row.id] !== row.status && (
            <button className="row-action" disabled={actionId === row.id} onClick={() => void updateStatus(`/api/tickets/${row.id}/status`, row.id, statusChanges[row.id])} type="button">Salvar</button>
          )}
        </td>
        <td>{formatDate(row.openedAt)}</td>
        <td className="customer-actions">
          {!['Closed', 'Cancelled'].includes(row.status) && <button className="row-action" onClick={() => onEdit(row)} type="button">Editar</button>}
          <button className="row-action" disabled={actionId === row.id} onClick={() => void showHistory(row.id, row.protocol, `/api/tickets/${row.id}/history`)} type="button">Histórico</button>
        </td>
      </tr>)}</tbody>
    </table></div>
  )
}

function MaintenanceTable({
  rows, installations, role, request, onDataChanged, onEdit, onDeactivate, deactivatingId, showHistory, actionId,
}: {
  rows: MaintenancePlan[]
  installations: Installation[]
  role: string
  request: ApiRequest
  onDataChanged: () => void
  onEdit: (row: MaintenancePlan) => void
  onDeactivate: (row: MaintenancePlan) => void
  deactivatingId: string | null
  showHistory: (id: string, label: string, path: string) => Promise<void>
  actionId: string | null
}) {
  const [executeId, setExecuteId] = useState<string | null>(null)
  const [executeNotes, setExecuteNotes] = useState('')
  const [executing, setExecuting] = useState(false)
  const [executeError, setExecuteError] = useState('')

  async function execute(plan: MaintenancePlan) {
    setExecuting(true)
    setExecuteError('')
    try {
      const response = await request(`/api/maintenance-plans/${plan.id}/execute`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ performedAt: new Date().toISOString(), serviceOrderId: null, notes: executeNotes || null }),
      })
      await readJson<unknown>(response, 'Não foi possível registrar a manutenção.')
      setExecuteId(null)
      setExecuteNotes('')
      onDataChanged()
    } catch (cause: unknown) {
      setExecuteError(cause instanceof Error ? cause.message : 'Não foi possível registrar a manutenção.')
    } finally {
      setExecuting(false)
    }
  }

  return (
    <>
      {executeError && <FeedbackMessage>{executeError}</FeedbackMessage>}
      <div className="customer-table-scroll"><table className="customer-table">
        <thead><tr><th>Plano</th><th>Instalação</th><th>Intervalo</th><th>Próxima data</th><th>Ações</th></tr></thead>
        <tbody>{rows.map((row) => <tr key={row.id}>
          <td><strong>{row.name}</strong><span>{row.notes || 'Sem observações'}</span></td>
          <td>{installationName(row.installationId, installations)}</td>
          <td>A cada {row.intervalDays} dias</td>
          <td>{formatDate(row.nextDueAt)}</td>
          <td className="customer-actions">
            <button className="row-action" disabled={actionId === row.id} onClick={() => void showHistory(row.id, row.name, `/api/maintenance-plans/${row.id}/history`)} type="button">Histórico</button>
            <button className="row-action" onClick={() => { setExecuteId(executeId === row.id ? null : row.id); setExecuteError('') }} type="button">Executar</button>
            <button className="row-action" onClick={() => onEdit(row)} type="button">Editar</button>
            {role !== 'Attendant' && <button className="row-action danger" disabled={deactivatingId === row.id} onClick={() => onDeactivate(row)} type="button">Desativar</button>}
          </td>
        </tr>)}</tbody>
      </table></div>
      {executeId && (
        <div className="maintenance-execute">
          <label className="customer-field">Observações da execução<textarea onChange={(event) => setExecuteNotes(event.target.value)} rows={2} value={executeNotes} /></label>
          <button className="primary-button" disabled={executing} onClick={() => {
            const plan = rows.find((item) => item.id === executeId)
            if (plan) void execute(plan)
          }} type="button">{executing ? 'Registrando...' : 'Confirmar execução'}</button>
        </div>
      )}
    </>
  )
}

function ServiceOrdersTable({
  rows, related, role, request, onDataChanged, updateStatus, actionId, showHistory, onEdit,
}: {
  rows: ServiceOrder[]
  related: RelatedData
  role: string
  request: ApiRequest
  onDataChanged: () => void
  updateStatus: (path: string, id: string, status: string) => Promise<void>
  actionId: string | null
  showHistory: (id: string, label: string, path: string) => Promise<void>
  onEdit: (row: ServiceOrder) => void
}) {
  const [completeId, setCompleteId] = useState<string | null>(null)
  const [report, setReport] = useState('')
  const [hours, setHours] = useState('')
  const [completionError, setCompletionError] = useState('')
  const [completing, setCompleting] = useState(false)
  const canEdit = role === 'CompanyAdmin' || role === 'Manager'

  async function complete(order: ServiceOrder) {
    setCompleting(true)
    setCompletionError('')
    try {
      const response = await request(`/api/service-orders/${order.id}/complete`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          hoursWorked: hours === '' ? null : Number(hours),
          technicalReport: report || null,
          checklist: order.checklist,
          materials: order.materials,
        }),
      })
      await readJson<unknown>(response, 'Não foi possível concluir a ordem de serviço.')
      setCompleteId(null)
      setReport('')
      setHours('')
      onDataChanged()
    } catch (cause: unknown) {
      setCompletionError(cause instanceof Error ? cause.message : 'Não foi possível concluir a ordem de serviço.')
    } finally {
      setCompleting(false)
    }
  }

  return (
    <>
      {completionError && <FeedbackMessage>{completionError}</FeedbackMessage>}
      <div className="customer-table-scroll"><table className="customer-table">
        <thead><tr><th>Serviço</th><th>Instalação</th><th>Agendamento</th><th>Status</th><th>Ações</th></tr></thead>
        <tbody>{rows.map((row) => <tr key={row.id}>
          <td><strong>{row.ticketId ? related.tickets.find((ticket) => ticket.id === row.ticketId)?.protocol ?? 'Ordem' : 'Ordem de serviço'}</strong><span>{row.description}</span></td>
          <td>{installationName(row.installationId, related.installations)}</td>
          <td>{row.scheduledAt ? new Date(row.scheduledAt).toLocaleString('pt-BR') : 'Sem agendamento'}</td>
          <td>
            <select
              aria-label={`Status da ordem ${row.id}`}
              disabled={role === 'Attendant' || row.status === 'Completed' || row.status === 'Cancelled'}
              onChange={(event) => void updateStatus(`/api/service-orders/${row.id}/status`, row.id, event.target.value)}
              value={row.status}
            >
              {orderStatuses.map(([value, label]) => <option disabled={value === 'Pending' || value === 'Completed'} key={value} value={value}>{label}</option>)}
            </select>
          </td>
          <td className="customer-actions">
            <button className="row-action" disabled={actionId === row.id} onClick={() => void showHistory(row.id, row.ticketId ? 'Ordem de serviço' : row.description, `/api/service-orders/${row.id}/history`)} type="button">Histórico</button>
            {canEdit && ['Pending', 'Scheduled', 'InProgress'].includes(row.status) && <button className="row-action" onClick={() => onEdit(row)} type="button">Editar</button>}
            {(role === 'CompanyAdmin' || role === 'Manager' || role === 'Technician') && ['Pending', 'Scheduled', 'InProgress'].includes(row.status) && (
              <button className="row-action" onClick={() => { setCompleteId(completeId === row.id ? null : row.id); setCompletionError('') }} type="button">Concluir</button>
            )}
          </td>
        </tr>)}</tbody>
      </table></div>
      {completeId && (
        <div className="maintenance-execute">
          <label className="customer-field">Horas trabalhadas<input min={0} onChange={(event) => setHours(event.target.value)} step={0.25} type="number" value={hours} /></label>
          <label className="customer-field">Relatório técnico<textarea onChange={(event) => setReport(event.target.value)} rows={3} value={report} /></label>
          <button className="primary-button" disabled={completing} onClick={() => {
            const order = rows.find((item) => item.id === completeId)
            if (order) void complete(order)
          }} type="button">{completing ? 'Concluindo...' : 'Confirmar conclusão'}</button>
        </div>
      )}
    </>
  )
}
