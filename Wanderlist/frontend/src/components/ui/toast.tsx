import { createContext, useCallback, useContext, useState } from 'react'
import { AlertCircle, CheckCircle2, X } from 'lucide-react'

interface ToastMessage {
  id: string
  text: string
  variant: 'error' | 'success'
}

interface ToastContextValue {
  showError: (text: string) => void
  showSuccess: (text: string) => void
}

const ToastContext = createContext<ToastContextValue | null>(null)

export function ToastProvider({ children }: { children: React.ReactNode }) {
  const [toasts, setToasts] = useState<ToastMessage[]>([])

  const dismiss = useCallback((id: string) => {
    setToasts((prev) => prev.filter((t) => t.id !== id))
  }, [])

  const push = useCallback((text: string, variant: 'error' | 'success') => {
    const id = crypto.randomUUID()
    setToasts((prev) => [...prev, { id, text, variant }])
    setTimeout(() => dismiss(id), 5000)
  }, [dismiss])

  const showError = useCallback((text: string) => push(text, 'error'), [push])
  const showSuccess = useCallback((text: string) => push(text, 'success'), [push])

  return (
    <ToastContext.Provider value={{ showError, showSuccess }}>
      {children}
      <div className="fixed bottom-4 right-4 z-50 flex flex-col gap-2 w-80">
        {toasts.map((t) => (
          <div
            key={t.id}
            className={`flex items-start gap-2 rounded-md border p-3 shadow-md text-sm ${
              t.variant === 'error' ? 'bg-destructive text-destructive-foreground border-destructive' : 'bg-background border-border'
            }`}
          >
            {t.variant === 'error' ? <AlertCircle className="h-4 w-4 mt-0.5 shrink-0" /> : <CheckCircle2 className="h-4 w-4 mt-0.5 shrink-0" />}
            <span className="flex-1">{t.text}</span>
            <button onClick={() => dismiss(t.id)}><X className="h-3.5 w-3.5" /></button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  )
}

export function useToast() {
  const ctx = useContext(ToastContext)
  if (!ctx) throw new Error('useToast must be used within a ToastProvider')
  return ctx
}

export function getErrorMessage(err: unknown): string {
  if (err && typeof err === 'object' && 'response' in err) {
    const res = (err as { response?: { data?: unknown; status?: number } }).response
    if (typeof res?.data === 'string') return res.data
    if (res?.data && typeof res.data === 'object' && 'title' in res.data) return String((res.data as { title: string }).title)
    if (res?.status) return `Request failed (${res.status})`
  }
  if (err instanceof Error) return err.message
  return 'Something went wrong'
}
