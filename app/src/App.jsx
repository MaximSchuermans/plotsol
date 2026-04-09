import { useCallback, useEffect, useRef, useState } from 'react'
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
  const [uploading, setUploading] = useState(false)
  const [uploadMessage, setUploadMessage] = useState('')
  const [uploadError, setUploadError] = useState('')
  const fileInputRef = useRef(null)
  const [files, setFiles] = useState([])
  const [isLoadingFiles, setIsLoadingFiles] = useState(false)
  const [filesError, setFilesError] = useState('')

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

  const handleUploadClick = () => {
    setUploadError('')
    setUploadMessage('')
    fileInputRef.current?.click()
  }

  const handleFileChange = async (event) => {
    const file = event.currentTarget.files?.[0]
    if (!file) {
      return
    }

    if (file.type !== 'application/pdf' && !file.name.toLowerCase().endsWith('.pdf')) {
      setUploadError('Only PDF files are supported at the moment.')
      event.currentTarget.value = ''
      return
    }

    setUploading(true)
    setUploadError('')
    setUploadMessage('')

    const formData = new FormData()
    formData.append('file', file)

    try {
      const response = await fetch('/files/upload', {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}` },
        body: formData,
      })

      if (!response.ok) {
        const payload = await response.json().catch(() => null)
        const message = payload?.message ?? 'Upload failed. Try again.'
        throw new Error(message)
      }

      const payload = await response.json().catch(() => null)
      setUploadMessage(`Uploaded ${payload?.fileName ?? file.name}`)
      // refresh file list after successful upload
      fetchFiles()
    } catch (error) {
      setUploadError(error instanceof Error ? error.message : 'Upload failed. Try again.')
    } finally {
      setUploading(false)
      event.currentTarget.value = ''
    }
  }

  const fetchFiles = async () => {
    if (!token) return

    setIsLoadingFiles(true)
    setFilesError('')
    try {
      const res = await fetch('/files', { headers: { Authorization: `Bearer ${token}` } })
      if (!res.ok) throw new Error('Unable to load files.')
      const data = await res.json()
      setFiles(data)
    } catch (err) {
      setFilesError(err instanceof Error ? err.message : String(err))
    } finally {
      setIsLoadingFiles(false)
    }
  }

  useEffect(() => {
    fetchFiles()
  }, [token])

  return (
    <div className="flex min-h-screen bg-slate-950 text-slate-100">
      <aside className="w-80 border-r border-white/5 bg-slate-900/80 p-6 shadow-[0_20px_60px_rgba(2,6,23,0.7)] backdrop-blur-lg">
        <div className="flex items-center justify-between text-[0.65rem] uppercase tracking-[0.45em] text-slate-500">
          <span>Explorer</span>
          <span className="text-emerald-400">ready</span>
        </div>
        <div className="mt-8 space-y-6">
          <div className="rounded-2xl border border-white/10 bg-white/5 p-5 shadow-inner">
            <p className="text-[0.65rem] uppercase tracking-[0.35em] text-slate-500">Upload</p>
            <p className="mt-1 text-sm text-slate-400">Add a single PDF and we store it securely.</p>
            <div className="mt-5 flex flex-col gap-3">
              <Button onClick={handleUploadClick} className="font-semibold" disabled={uploading}>
                {uploading ? 'Uploading…' : 'Upload PDF'}
              </Button>
              {uploadMessage && <p className="text-xs text-emerald-300">{uploadMessage}</p>}
              {uploadError && <p className="text-xs text-destructive-foreground">{uploadError}</p>}
              <input
                ref={fileInputRef}
                type="file"
                accept="application/pdf"
                className="hidden"
                onChange={handleFileChange}
              />
            </div>
          </div>
          <div className="rounded-2xl border border-dashed border-white/20 bg-slate-950/60 p-5 text-[0.65rem] uppercase tracking-[0.35em] text-slate-500">
            Files are copied into Azure Blob Storage and metadata lands in MongoDB.
          </div>

          <div className="mt-6">
            <p className="text-[0.65rem] uppercase tracking-[0.35em] text-slate-500">Your PDFs</p>
            {isLoadingFiles ? (
              <p className="mt-2 text-sm text-slate-400">Loading files…</p>
            ) : filesError ? (
              <p className="mt-2 text-sm text-destructive-foreground">{filesError}</p>
            ) : files.length ? (
              <ul className="mt-2 space-y-1">
                {files.map((f) => (
                  <li key={f.id}>
                    <a
                      href={f.blobUri}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="text-slate-100 hover:underline"
                    >
                      {f.fileName}
                    </a>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="mt-2 text-sm text-slate-400">No files uploaded yet.</p>
            )}
          </div>
        </div>
      </aside>
      <main className="flex-1 bg-gradient-to-br from-slate-950 via-slate-900 to-slate-950 p-10">
        <div className="mx-auto flex max-w-3xl flex-col gap-6 rounded-[36px] border border-white/10 bg-slate-900/70 p-10 shadow-[0_40px_60px_rgba(2,6,23,0.85)] md:p-12">
          <div className="flex flex-col gap-6 md:flex-row md:items-center md:justify-between">
            <div>
              <p className="text-xs uppercase tracking-[0.4em] text-slate-500" aria-live="polite">
                Workspace
              </p>
              <h1 className="text-4xl font-semibold text-white">Welcome back, {displayName}</h1>
              <p className="mt-2 text-sm text-slate-400">Drop a PDF on the left to start building your workspace.</p>
            </div>
            <Button variant="ghost" onClick={handleLogout} className="text-slate-200 hover:bg-white/10">
              Sign out
            </Button>
          </div>
          <p className="text-sm text-slate-400">
            Everything you upload is archived in Azure Blob Storage while we keep the file metadata, ownership, and audit trail in MongoDB.
          </p>
        </div>
      </main>
    </div>
  )
}

export default App
