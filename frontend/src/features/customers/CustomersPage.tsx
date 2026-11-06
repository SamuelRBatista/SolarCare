import { FeedbackMessage } from '../../components/FeedbackMessage'
import { useCustomers, type CustomerInput } from './useCustomers'

type CustomersPageProps = {
  request: (path: string, init?: RequestInit) => Promise<Response>
  onSessionExpired: () => void
  onDataChanged: () => void
  refreshVersion: number
  role: string
}

export function CustomersPage({
  request,
  onSessionExpired,
  onDataChanged,
  refreshVersion,
  role,
}: CustomersPageProps) {
  const {
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
  } = useCustomers({ request, onSessionExpired, onDataChanged, refreshVersion })

  return (
    <section className="customers-section">
      <div className="customers-toolbar">
        <form className="customer-search" onSubmit={applySearch} role="search">
          <label className="visually-hidden" htmlFor="customer-search">Buscar clientes</label>
          <input
            id="customer-search"
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Buscar por nome ou e-mail"
            value={search}
          />
          <button className="secondary-button" type="submit">Buscar</button>
        </form>
        <button className="primary-button" onClick={startCreation} type="button">+ Novo cliente</button>
      </div>

      {error && <FeedbackMessage>{error}</FeedbackMessage>}

      {formOpen && (
        <form className="panel customer-form" onSubmit={(event) => void submit(event)}>
          <div className="customer-form-heading">
            <div>
              <h2>{editingId ? 'Editar cliente' : 'Novo cliente'}</h2>
              <p>Os campos CPF/CNPJ são armazenados de forma protegida.</p>
            </div>
            <button
              className="secondary-button"
              onClick={closeForm}
              type="button"
            >
              Cancelar
            </button>
          </div>

          <div className="customer-form-grid">
            <label className="customer-field customer-field-wide">
              Nome *
              <input
                autoComplete="name"
                maxLength={160}
                onChange={(event) => setForm({ ...form, name: event.target.value })}
                required
                value={form.name}
              />
            </label>
            <label className="customer-field">
              Tipo *
              <select
                onChange={(event) => setForm({ ...form, kind: event.target.value as CustomerInput['kind'] })}
                value={form.kind}
              >
                <option value="Person">Pessoa física</option>
                <option value="Company">Pessoa jurídica</option>
              </select>
            </label>
            <label className="customer-field">
              CPF/CNPJ
              <input
                autoComplete="off"
                maxLength={18}
                onChange={(event) => setForm({ ...form, taxIdentifier: event.target.value })}
                placeholder="Somente números ou formatado"
                value={form.taxIdentifier ?? ''}
              />
            </label>
            <label className="customer-field">
              Telefone
              <input
                autoComplete="tel"
                maxLength={40}
                onChange={(event) => setForm({ ...form, phone: event.target.value })}
                value={form.phone ?? ''}
              />
            </label>
            <label className="customer-field">
              E-mail
              <input
                autoComplete="email"
                maxLength={254}
                onChange={(event) => setForm({ ...form, email: event.target.value })}
                type="email"
                value={form.email ?? ''}
              />
            </label>
            <label className="customer-field customer-field-wide">
              Endereço
              <input
                autoComplete="street-address"
                maxLength={240}
                onChange={(event) => setForm({ ...form, address: event.target.value })}
                value={form.address ?? ''}
              />
            </label>
            <label className="customer-field">
              Cidade
              <input
                autoComplete="address-level2"
                maxLength={120}
                onChange={(event) => setForm({ ...form, city: event.target.value })}
                value={form.city ?? ''}
              />
            </label>
            <label className="customer-field">
              Estado
              <input
                autoComplete="address-level1"
                maxLength={2}
                onChange={(event) => setForm({ ...form, state: event.target.value.toUpperCase() })}
                placeholder="UF"
                value={form.state ?? ''}
              />
            </label>
            <label className="customer-field customer-field-wide">
              Observações
              <textarea
                maxLength={4000}
                onChange={(event) => setForm({ ...form, notes: event.target.value })}
                rows={3}
                value={form.notes ?? ''}
              />
            </label>
          </div>

          {formError && <FeedbackMessage>{formError}</FeedbackMessage>}
          <div className="customer-form-actions">
            <button className="primary-button" disabled={saving} type="submit">
              {saving ? 'Salvando...' : editingId ? 'Salvar alterações' : 'Cadastrar cliente'}
            </button>
          </div>
        </form>
      )}

      <div className="panel customer-list-panel">
        {loading ? (
          <FeedbackMessage variant="status">Carregando clientes...</FeedbackMessage>
        ) : customers.length === 0 ? (
          <div className="customer-list-message">
            <strong>{appliedSearch ? 'Nenhum cliente encontrado' : 'Nenhum cliente cadastrado'}</strong>
            <span>
              {appliedSearch
                ? 'Tente outra busca ou limpe o filtro.'
                : 'Cadastre o primeiro cliente para começar a organizar sua operação.'}
            </span>
            {appliedSearch && (
              <button className="text-button" onClick={clearSearch} type="button">Limpar busca</button>
            )}
          </div>
        ) : (
          <>
            <div className="customer-table-scroll">
              <table className="customer-table">
                <thead>
                  <tr>
                    <th scope="col">Cliente</th>
                    <th scope="col">Documento</th>
                    <th scope="col">Contato</th>
                    <th scope="col">Localização</th>
                    <th scope="col"><span className="visually-hidden">Ações</span></th>
                  </tr>
                </thead>
                <tbody>
                  {customers.map((customer) => (
                    <tr key={customer.id}>
                      <td>
                        <strong>{customer.name}</strong>
                        <span>{customer.kind === 'Person' ? 'Pessoa física' : 'Pessoa jurídica'}</span>
                      </td>
                      <td>{customer.taxIdentifier || '—'}</td>
                      <td>
                        <span>{customer.email || '—'}</span>
                        <span>{customer.phone || ''}</span>
                      </td>
                      <td>
                        <span>{[customer.city, customer.state].filter(Boolean).join(' / ') || '—'}</span>
                        <span>{customer.address || ''}</span>
                      </td>
                      <td className="customer-actions">
                        <button className="row-action" onClick={() => startEdit(customer)} type="button">Editar</button>
                        {(role === 'CompanyAdmin' || role === 'Manager') && (
                          <button
                            className="row-action danger"
                            disabled={deactivatingId === customer.id}
                            onClick={() => void deactivate(customer)}
                            type="button"
                          >
                            {deactivatingId === customer.id ? 'Desativando...' : 'Desativar'}
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="customer-pagination">
              <span>Exibindo {offset + 1}–{offset + customers.length} clientes</span>
              <div>
                <button
                  className="secondary-button"
                  disabled={offset === 0 || loading}
                  onClick={() => setOffset((currentOffset) => Math.max(0, currentOffset - 50))}
                  type="button"
                >
                  Anterior
                </button>
                <button
                  className="secondary-button"
                  disabled={customers.length < 50 || loading}
                  onClick={() => setOffset((currentOffset) => currentOffset + 50)}
                  type="button"
                >
                  Próxima
                </button>
              </div>
            </div>
          </>
        )}
      </div>
    </section>
  )
}
