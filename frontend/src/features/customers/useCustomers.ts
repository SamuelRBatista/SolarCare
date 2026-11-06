import { useEffect, useState, type FormEvent } from 'react'
import { getResponseError, SessionExpiredError } from '../../shared/api'

export type Customer = {
  id: string
  name: string
  kind: 'Person' | 'Company'
  taxIdentifier: string | null
  phone: string | null
  email: string | null
  address: string | null
  city: string | null
  state: string | null
  notes: string | null
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export type CustomerInput = Pick<
  Customer,
  'name' | 'kind' | 'taxIdentifier' | 'phone' | 'email' | 'address' | 'city' | 'state' | 'notes'
>

export type CustomerRequest = (path: string, init?: RequestInit) => Promise<Response>

const emptyCustomer: CustomerInput = {
  name: '',
  kind: 'Person',
  taxIdentifier: '',
  phone: '',
  email: '',
  address: '',
  city: '',
  state: '',
  notes: '',
}

export function useCustomers({
  request,
  onSessionExpired,
  onDataChanged,
  refreshVersion,
}: {
  request: CustomerRequest
  onSessionExpired: () => void
  onDataChanged: () => void
  refreshVersion: number
}) {
  const [customers, setCustomers] = useState<Customer[]>([])
  const [loadedKey, setLoadedKey] = useState('')
  const [error, setError] = useState('')
  const [search, setSearch] = useState('')
  const [appliedSearch, setAppliedSearch] = useState('')
  const [offset, setOffset] = useState(0)
  const [form, setForm] = useState<CustomerInput>(emptyCustomer)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [formOpen, setFormOpen] = useState(false)
  const [formError, setFormError] = useState('')
  const [saving, setSaving] = useState(false)
  const [deactivatingId, setDeactivatingId] = useState<string | null>(null)
  const loadKey = `${appliedSearch}:${offset}:${refreshVersion}`
  const loading = loadedKey !== loadKey

  useEffect(() => {
    const controller = new AbortController()
    const query = new URLSearchParams({ limit: '50', offset: String(offset) })
    if (appliedSearch) {
      query.set('search', appliedSearch)
    }

    request(`/api/customers?${query}`, { signal: controller.signal })
      .then(async (response) => {
        if (!response.ok) {
          throw new Error(`Não foi possível carregar os clientes (HTTP ${response.status}).`)
        }
        setCustomers((await response.json()) as Customer[])
        setError('')
        setLoadedKey(loadKey)
      })
      .catch((cause: unknown) => {
        if (cause instanceof DOMException && cause.name === 'AbortError') {
          return
        }
        if (cause instanceof SessionExpiredError) {
          onSessionExpired()
        }
        setError(cause instanceof Error ? cause.message : 'Não foi possível carregar os clientes.')
        setLoadedKey(loadKey)
      })

    return () => controller.abort()
  }, [appliedSearch, loadKey, offset, onSessionExpired, request, refreshVersion])

  function startCreation() {
    setEditingId(null)
    setForm(emptyCustomer)
    setFormError('')
    setFormOpen(true)
  }

  function startEdit(customer: Customer) {
    setEditingId(customer.id)
    setForm({
      name: customer.name,
      kind: customer.kind,
      taxIdentifier: customer.taxIdentifier ?? '',
      phone: customer.phone ?? '',
      email: customer.email ?? '',
      address: customer.address ?? '',
      city: customer.city ?? '',
      state: customer.state ?? '',
      notes: customer.notes ?? '',
    })
    setFormError('')
    setFormOpen(true)
  }

  function closeForm() {
    setFormOpen(false)
    setFormError('')
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSaving(true)
    setFormError('')

    try {
      const response = await request(
        editingId ? `/api/customers/${editingId}` : '/api/customers',
        {
          method: editingId ? 'PUT' : 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            ...form,
            taxIdentifier: form.taxIdentifier?.trim() || null,
            phone: form.phone?.trim() || null,
            email: form.email?.trim() || null,
            address: form.address?.trim() || null,
            city: form.city?.trim() || null,
            state: form.state?.trim() || null,
            notes: form.notes?.trim() || null,
          }),
        },
      )

      if (!response.ok) {
        throw await getResponseError(response, 'Não foi possível salvar o cliente.')
      }

      setFormOpen(false)
      setEditingId(null)
      setForm(emptyCustomer)
      onDataChanged()
    } catch (cause: unknown) {
      if (cause instanceof SessionExpiredError) {
        onSessionExpired()
      }
      setFormError(cause instanceof Error ? cause.message : 'Não foi possível salvar o cliente.')
    } finally {
      setSaving(false)
    }
  }

  async function deactivate(customer: Customer) {
    if (!window.confirm(`Deseja desativar o cliente ${customer.name}?`)) {
      return
    }

    setDeactivatingId(customer.id)
    setError('')
    try {
      const response = await request(`/api/customers/${customer.id}`, { method: 'DELETE' })
      if (!response.ok) {
        throw await getResponseError(response, 'Não foi possível desativar o cliente.')
      }

      if (customers.length === 1 && offset > 0) {
        setOffset((currentOffset) => Math.max(0, currentOffset - 50))
      }
      onDataChanged()
    } catch (cause: unknown) {
      if (cause instanceof SessionExpiredError) {
        onSessionExpired()
      }
      setError(cause instanceof Error ? cause.message : 'Não foi possível desativar o cliente.')
    } finally {
      setDeactivatingId(null)
    }
  }

  function applySearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setOffset(0)
    setAppliedSearch(search.trim())
  }

  function clearSearch() {
    setSearch('')
    setAppliedSearch('')
    setOffset(0)
  }

  return {
    customers,
    loading,
    error,
    search,
    setSearch,
    appliedSearch,
    offset,
    setOffset,
    form,
    setForm,
    editingId,
    formOpen,
    setFormOpen,
    formError,
    saving,
    deactivatingId,
    startCreation,
    startEdit,
    closeForm,
    submit,
    deactivate,
    applySearch,
    clearSearch,
  }
}
