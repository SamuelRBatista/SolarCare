import type { ReactNode } from 'react'

type FeedbackMessageProps = {
  children: ReactNode
  variant?: 'error' | 'status'
  className?: string
}

export function FeedbackMessage({
  children,
  variant = 'error',
  className = '',
}: FeedbackMessageProps) {
  const classes = [variant === 'error' ? 'operation-error' : 'customer-list-message', className]
    .filter(Boolean)
    .join(' ')

  return (
    <div className={classes} role={variant === 'error' ? 'alert' : 'status'}>
      {children}
    </div>
  )
}
