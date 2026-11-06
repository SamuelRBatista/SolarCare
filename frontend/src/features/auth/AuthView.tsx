import { useState, type FormEvent } from 'react'
import type { ApiStatus, AuthFields, AuthMode } from '../../shared/authTypes'

type AuthViewProps = {
  apiStatus: ApiStatus
  error: string
  mode: AuthMode
  onModeChange: (mode: AuthMode) => void
  onSubmit: (fields: AuthFields) => Promise<void>
  submitting: boolean
}

export function AuthView({
  apiStatus,
  error,
  mode,
  onModeChange,
  onSubmit,
  submitting,
}: AuthViewProps) {
  const [fields, setFields] = useState<AuthFields>({
    companyName: '',
    fullName: '',
    email: '',
    password: '',
  })

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    void onSubmit(fields)
  }

  return (
    <main className="auth-page">
      <div className="auth-brand">
        <span className="brand-mark" aria-hidden="true">S</span>
        <span className="brand-name">solarcare<span>.</span></span>
      </div>
      <div className="auth-card">
        <div className="auth-eyebrow">GESTÃO DE PÓS-VENDA SOLAR</div>
        <h1>{mode === 'register' ? 'Crie seu espaço de trabalho' : 'Bem-vindo de volta'}</h1>
        <p className="auth-intro">
          {mode === 'register'
            ? 'Cadastre sua empresa e comece a organizar a operação.'
            : 'Entre para acessar o espaço de trabalho da sua empresa.'}
        </p>

        <div className="auth-tabs" role="tablist" aria-label="Acesso à conta">
          <button
            aria-selected={mode === 'login'}
            className={mode === 'login' ? 'selected' : ''}
            onClick={() => onModeChange('login')}
            role="tab"
            type="button"
          >
            Entrar
          </button>
          <button
            aria-selected={mode === 'register'}
            className={mode === 'register' ? 'selected' : ''}
            onClick={() => onModeChange('register')}
            role="tab"
            type="button"
          >
            Criar conta
          </button>
        </div>

        <form className="auth-form" onSubmit={submit}>
          {mode === 'register' && (
            <>
              <label>
                Nome da empresa
                <input
                  autoComplete="organization"
                  maxLength={160}
                  onChange={(event) => setFields({ ...fields, companyName: event.target.value })}
                  required
                  value={fields.companyName}
                />
              </label>
              <label>
                Seu nome
                <input
                  autoComplete="name"
                  maxLength={160}
                  onChange={(event) => setFields({ ...fields, fullName: event.target.value })}
                  required
                  value={fields.fullName}
                />
              </label>
            </>
          )}
          <label>
            E-mail
            <input
              autoComplete="email"
              maxLength={254}
              onChange={(event) => setFields({ ...fields, email: event.target.value })}
              required
              type="email"
              value={fields.email}
            />
          </label>
          <label>
            Senha
            <input
              autoComplete={mode === 'register' ? 'new-password' : 'current-password'}
              maxLength={128}
              minLength={mode === 'register' ? 12 : 1}
              onChange={(event) => setFields({ ...fields, password: event.target.value })}
              required
              type="password"
              value={fields.password}
            />
            {mode === 'register' && <span className="field-hint">Use pelo menos 12 caracteres.</span>}
          </label>

          {error && <div className="auth-error" role="alert">{error}</div>}

          <button className="auth-submit" disabled={submitting} type="submit">
            {submitting
              ? 'Aguarde...'
              : mode === 'register'
                ? 'Criar conta e empresa'
                : 'Entrar'}
          </button>
        </form>

        <p className="auth-disclaimer">
          {mode === 'register'
            ? 'Ao criar a conta, você será o administrador inicial da empresa.'
            : 'O acesso é protegido por sessão segura e expira após inatividade.'}
        </p>
        <div className={`auth-api-status ${apiStatus}`} role="status">
          <span className="status-dot" />
          {apiStatus === 'checking' && 'Conectando à plataforma'}
          {apiStatus === 'online' && 'Plataforma disponível'}
          {apiStatus === 'offline' && 'API indisponível'}
        </div>
      </div>
      <footer className="auth-footer">SolarCare · Gestão de pós-venda para energia solar</footer>
    </main>
  )
}
