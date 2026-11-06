import { useEffect, useState, type FormEvent } from 'react'

export type OperationPage = 'Instalações' | 'Chamados' | 'Ordens de serviço' | 'Manutenção'
export type ApiRequest = (path: string, init?: RequestInit) => Promise<Response>

export type Customer = { id: string; name: string }
export type Installation = {
  id: string
  customerId: string
  name: string
  city: string | null
  state: string | null
  installedAt: string | null
  capacityKwp: number | null
  moduleCount: number | null
  moduleManufacturer: string | null
  moduleModel: string | null
  inverterManufacturer: string | null
  inverterModel: string | null
  address: string | null
  integratorName: string | null
  technicalResponsible: string | null
  notes: string | null
  isActive: boolean
}
export type Equipment = { id: string; installationId: string; manufacturer: string; model: string | null }
export type Ticket = {
  id: string
  protocol: string
  customerId: string
  installationId: string | null
  equipmentId: string | null
  description: string
  category: string
  priority: string
  status: string
  assignedUserId: string | null
  openedAt: string
}
export type ServiceOrder = {
  id: string
  ticketId: string | null
  installationId: string
  technicianUserId: string | null
  scheduledAt: string | null
  address: string | null
  description: string
  status: string
  checklist: { description: string; isComplete: boolean }[]
  materials: { description: string; quantity: number; unit: string | null }[]
  hoursWorked: number | null
  technicalReport: string | null
}
export type MaintenancePlan = {
  id: string
  installationId: string
  name: string
  intervalDays: number
  nextDueAt: string
  isActive: boolean
  notes: string | null
}
export type RelatedData = {
  customers: Customer[]
  installations: Installation[]
  equipment: Equipment[]
  tickets: Ticket[]
}

export type OperationHistory = {
  id: string
  label: string
  entries: Record<string, unknown>[]
}

const emptyRelatedData: RelatedData = {
  customers: [],
  installations: [],
  equipment: [],
  tickets: [],
}

export async function responseError(response: Response, fallback: string): Promise<Error> {
  let detail = ''
  try {
    const body = (await response.json()) as {
      detail?: string
      errors?: Record<string, unknown>
      title?: string
    }
    detail = body.detail ?? ''
    if (!detail && body.errors) {
      for (const value of Object.values(body.errors)) {
        if (typeof value === 'string') {
          detail = value
          break
        }
        if (Array.isArray(value)) {
          const message = value.find((item): item is string => typeof item === 'string')
          if (message) {
            detail = message
            break
          }
        }
      }
    }
    detail ||= body.title ?? ''
  } catch {
    detail = ''
  }
  return new Error(detail || `${fallback} (HTTP ${response.status}).`)
}

export async function readJson<T>(response: Response, fallback: string): Promise<T> {
  if (!response.ok) throw await responseError(response, fallback)
  return (await response.json()) as T
}

export function useOperations({
  page,
  request,
  onDataChanged,
  refreshVersion,
}: {
  page: OperationPage
  request: ApiRequest
  onDataChanged: () => void
  refreshVersion: number
}) {
  const [rows, setRows] = useState<unknown[]>([])
  const [related, setRelated] = useState<RelatedData>(emptyRelatedData)
  const [loadState, setLoadState] = useState<{ key: string; error: string }>({ key: '', error: '' })
  const [error, setError] = useState('')
  const [formOpen, setFormOpen] = useState(false)
  const [saving, setSaving] = useState(false)
  const [deactivatingId, setDeactivatingId] = useState<string | null>(null)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [formError, setFormError] = useState('')
  const [statusChanges, setStatusChanges] = useState<Record<string, string>>({})
  const [history, setHistory] = useState<OperationHistory | null>(null)
  const [actionId, setActionId] = useState<string | null>(null)

  const isInstallations = page === 'Instalações'
  const isTickets = page === 'Chamados'
  const isOrders = page === 'Ordens de serviço'
  const isMaintenance = page === 'Manutenção'
  const endpoint = isInstallations
    ? '/api/installations?limit=100&offset=0'
    : isTickets
      ? '/api/tickets?limit=100&offset=0'
      : isOrders
        ? '/api/service-orders?limit=100&offset=0'
        : '/api/maintenance-plans?includeInactive=false&limit=100&offset=0'
  const loadKey = `${page}:${refreshVersion}`
  const loading = loadState.key !== loadKey

  useEffect(() => {
    const controller = new AbortController()
    const requests: Promise<unknown>[] = [
      request(endpoint, { signal: controller.signal }).then((response) =>
        readJson<unknown[]>(response, `Não foi possível carregar ${page.toLowerCase()}.`),
      ),
    ]

    if (isInstallations || isTickets || isMaintenance || isOrders) {
      requests.push(
        request('/api/customers?limit=100&offset=0', { signal: controller.signal }).then((response) =>
          readJson<Customer[]>(response, 'Não foi possível carregar os clientes relacionados.'),
        ),
      )
    }
    if (isTickets || isMaintenance || isOrders) {
      requests.push(
        request('/api/installations?limit=100&offset=0', { signal: controller.signal }).then((response) =>
          readJson<Installation[]>(response, 'Não foi possível carregar as instalações relacionadas.'),
        ),
      )
    }
    if (isTickets) {
      requests.push(
        request('/api/equipment?limit=100&offset=0&warrantiesOnly=false', { signal: controller.signal }).then((response) =>
          readJson<Equipment[]>(response, 'Não foi possível carregar os equipamentos relacionados.'),
        ),
      )
    }
    if (isOrders) {
      requests.push(
        request('/api/tickets?limit=100&offset=0', { signal: controller.signal }).then((response) =>
          readJson<Ticket[]>(response, 'Não foi possível carregar os chamados relacionados.'),
        ),
      )
    }

    Promise.all(requests)
      .then((results) => {
        if (controller.signal.aborted) return
        setRows(results[0] as unknown[])
        const relatedData = { ...emptyRelatedData }
        let index = 1
        if (isInstallations || isTickets || isMaintenance || isOrders) {
          relatedData.customers = results[index++] as Customer[]
        }
        if (isTickets || isMaintenance || isOrders) {
          relatedData.installations = results[index++] as Installation[]
        }
        if (isTickets) relatedData.equipment = results[index++] as Equipment[]
        if (isOrders) relatedData.tickets = results[index] as Ticket[]
        setRelated(relatedData)
        setLoadState({ key: loadKey, error: '' })
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted) return
        setLoadState({
          key: loadKey,
          error: cause instanceof Error ? cause.message : 'Não foi possível carregar os registros.',
        })
      })

    return () => controller.abort()
  }, [endpoint, isInstallations, isMaintenance, isOrders, isTickets, loadKey, page, request])

  function openCreateForm() {
    setEditingId(null)
    setFormError('')
    setFormOpen(true)
  }

  function openEditForm(id: string) {
    setEditingId(id)
    setFormError('')
    setFormOpen(true)
  }

  function closeForm() {
    setFormOpen(false)
    setFormError('')
  }

  async function submit(
    event: FormEvent<HTMLFormElement>,
    path: string,
    method: 'POST' | 'PUT',
    payload: unknown,
  ) {
    event.preventDefault()
    setSaving(true)
    setFormError('')
    try {
      const response = await request(path, {
        method,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      })
      await readJson<unknown>(response, `Não foi possível salvar ${page.toLowerCase()}.`)
      closeForm()
      setEditingId(null)
      onDataChanged()
    } catch (cause: unknown) {
      setFormError(cause instanceof Error ? cause.message : 'Não foi possível salvar o registro.')
    } finally {
      setSaving(false)
    }
  }

  async function deactivate(id: string, name: string) {
    if (!window.confirm(`Deseja desativar "${name}"?`)) return
    setDeactivatingId(id)
    setError('')
    try {
      const response = await request(`${endpoint.split('?')[0]}/${id}`, { method: 'DELETE' })
      if (!response.ok) throw await responseError(response, 'Não foi possível desativar o registro.')
      onDataChanged()
    } catch (cause: unknown) {
      setError(cause instanceof Error ? cause.message : 'Não foi possível desativar o registro.')
    } finally {
      setDeactivatingId(null)
    }
  }

  async function updateStatus(path: string, id: string, status: string) {
    setActionId(id)
    setError('')
    try {
      const response = await request(path, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ status, note: null }),
      })
      await readJson<unknown>(response, 'Não foi possível atualizar o status.')
      onDataChanged()
    } catch (cause: unknown) {
      setError(cause instanceof Error ? cause.message : 'Não foi possível atualizar o status.')
    } finally {
      setActionId(null)
    }
  }

  async function showHistory(id: string, name: string, path: string) {
    setActionId(id)
    setError('')
    try {
      const response = await request(path)
      const entries = await readJson<Record<string, unknown>[]>(response, 'Não foi possível carregar o histórico.')
      setHistory({ id, label: name, entries })
    } catch (cause: unknown) {
      setError(cause instanceof Error ? cause.message : 'Não foi possível carregar o histórico.')
    } finally {
      setActionId(null)
    }
  }

  return {
    rows,
    related,
    loadState,
    error,
    setError,
    formOpen,
    setFormOpen,
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
    endpoint,
    loadKey,
    loading,
    openCreateForm,
    openEditForm,
    closeForm,
    submit,
    deactivate,
    updateStatus,
    showHistory,
  }
}
