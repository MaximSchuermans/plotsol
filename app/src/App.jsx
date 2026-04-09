import { useCallback, useEffect, useState } from 'react'
import LoginForm from './components/login-form'
import { Button } from '@/components/ui/button'
import './App.css'

const TOKEN_STORAGE_KEY = 'plotsol-auth-token'
const USER_STORAGE_KEY = 'plotsol-auth-user'

function App() {
  const [token, setToken] = useState(() => localStorage.getItem(TOKEN_STORAGE_KEY))
  const [username, setUsername] = useState(() => localStorage.getItem(USER_STORAGE_KEY))
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [authError, setAuthError] = useState('')

  useEffect(() => {
    if (token) {
      localStorage.setItem(TOKEN_STORAGE_KEY, token)
    } else {
      localStorage.removeItem(TOKEN_STORAGE_KEY)
    }
  }, [token])

  useEffect(() => {
    if (username) {
      localStorage.setItem(USER_STORAGE_KEY, username)
    } else {
      localStorage.removeItem(USER_STORAGE_KEY)
    }
  }, [username])

  const handleLogout = useCallback(() => {
    setToken(null)
    setUsername(null)
    setAuthError('')
  }, [])

  const handleLogin = async ({ username: suppliedUsername, password }) => {
    setAuthError('')
    setIsSubmitting(true)
    try {
      const response = await fetch('/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username: suppliedUsername, password }),
      })

      if (!response.ok) {
        const payload = await response.json().catch(() => null)
        const message = payload?.message ?? 'Unable to sign in.'
        throw new Error(message)
      }

      const data = await response.json()
      setToken(data.token)
      setUsername(data.username ?? suppliedUsername)
    } catch (error) {
      setAuthError(error instanceof Error ? error.message : 'Unable to sign in.')
    } finally {
      setIsSubmitting(false)
    }
  }

  if (!token) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-950 px-4 py-10">
        <div className="w-full max-w-md">
          <LoginForm onSubmit={handleLogin} isLoading={isSubmitting} error={authError} />
        </div>
      </div>
    )
  }

  const displayName = username ?? 'Explorer'
  const tips = [
    'Connect your Mongo Atlas cluster',
    'Draft your first project',
    'Drop folders or files',
  ]

  return (
    <div className="flex min-h-screen bg-slate-950 text-slate-100">
      <aside className="w-72 border-r border-white/10 bg-slate-900/70 p-6 backdrop-blur-lg">
        <div className="flex items-center justify-between text-[0.65rem] uppercase tracking-[0.45em] text-slate-500">
          <span>Explorer</span>
          <span className="text-emerald-400">idle</span>
        </div>
        <div className="mt-8 space-y-5">
          <div className="rounded-2xl border border-white/10 bg-white/5 p-4">
            <p className="text-[0.65rem] uppercase tracking-[0.35em] text-slate-500">Folders</p>
            <div className="mt-6 flex items-start justify-between gap-4">
              <div>
                <p className="text-lg font-semibold text-slate-100">No files yet</p>
                <p className="text-xs text-slate-500">Create your first folder to begin.</p>
              </div>
              <span className="rounded-full bg-gradient-to-br from-emerald-400/30 to-emerald-200/40 px-3 py-1 text-[0.6rem] font-semibold uppercase tracking-[0.35em] text-emerald-200">
                empty
              </span>
            </div>
          </div>
          <div className="rounded-2xl border border-dashed border-white/20 bg-slate-950/60 p-4 text-[0.65rem] uppercase tracking-[0.35em] text-slate-500">
            No sources configured
          </div>
        </div>
      </aside>
      <main className="flex-1 bg-gradient-to-br from-slate-950 via-slate-900 to-slate-950 p-10">
        <header className="flex flex-col gap-4 md:flex-row md:items-start md:justify-between">
          <div>
            <p className="text-xs uppercase tracking-[0.4em] text-slate-500">Workspace</p>
            <h1 className="text-4xl font-semibold text-white">Welcome back, {displayName}</h1>
            <p className="text-sm text-slate-400">Once you connect MongoDB Atlas you’ll see your files here.</p>
          </div>
          <Button variant="ghost" onClick={handleLogout} className="text-slate-200 hover:bg-white/10">
            Sign out
          </Button>
        </header>
        <section className="mt-10 rounded-[32px] border border-white/10 bg-white/5 p-8 shadow-2xl shadow-slate-900/60">
          <div className="flex items-center justify-between text-xs uppercase tracking-[0.4em] text-slate-500">
            <span>Explorer status</span>
            <span className="text-emerald-300">Awaiting files</span>
          </div>
          <div className="mt-6 rounded-3xl border border-dashed border-white/20 bg-slate-900/70 p-7 text-center text-sm text-slate-400">
            <p className="text-base font-semibold text-slate-200">Your file tree is empty</p>
            <p className="mt-1 text-xs text-slate-500">Once you sync or upload folders the structure will appear here.</p>
          </div>
        </section>
        <section className="mt-8 grid gap-5 lg:grid-cols-3">
          {tips.map((tip) => (
            <article key={tip} className="rounded-2xl border border-white/5 bg-slate-900/60 p-5">
              <p className="text-[0.65rem] uppercase tracking-[0.3em] text-slate-500">Next step</p>
              <p className="mt-3 text-lg font-semibold text-slate-100">{tip}</p>
              <p className="mt-2 text-sm text-slate-500">This card will grow rich once files land in your workspace.</p>
            </article>
          ))}
        </section>
      </main>
    </div>
  )
}

export default App
