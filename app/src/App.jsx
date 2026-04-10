import { useCallback, useEffect, useRef, useState } from 'react'
import { FileText, ExternalLink, Clock, Loader2, Trash2, Eye, PanelLeftClose, PanelLeftOpen } from 'lucide-react'
import LoginForm from './components/login-form'
import { Button } from '@/components/ui/button'
import PdfViewer from './components/pdf-viewer'
import DocumentChatPanel from './components/document-chat-panel'
import './App.css'

const TOKEN_STORAGE_KEY = 'plotsol-auth-token'
const USER_STORAGE_KEY = 'plotsol-auth-user'

function App() {
  const [token, setToken] = useState(() => localStorage.getItem(TOKEN_STORAGE_KEY))
  const [username, setUsername] = useState(() => localStorage.getItem(USER_STORAGE_KEY))
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [authError, setAuthError] = useState('')
  const [uploading, setUploading] = useState(false)
  const [uploadProgress, setUploadProgress] = useState(0)
  const [uploadMessage, setUploadMessage] = useState('')
  const [uploadError, setUploadError] = useState('')
  const fileInputRef = useRef(null)
  const [files, setFiles] = useState([])
  const [isLoadingFiles, setIsLoadingFiles] = useState(false)
  const [filesError, setFilesError] = useState('')
  const [selectedFile, setSelectedFile] = useState(null)
  const [deletingId, setDeletingId] = useState(null)
  const [isExplorerCollapsed, setIsExplorerCollapsed] = useState(false)

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

  // Fetch user files; logout on 401
  const fetchFiles = useCallback(async () => {
    if (!token) return
    setIsLoadingFiles(true)
    setFilesError('')
    try {
      const res = await fetch('/files', { headers: { Authorization: `Bearer ${token}` } })
      if (res.status === 401) {
        handleLogout()
        return
      }
      if (!res.ok) throw new Error('Unable to load files.')
      const data = await res.json()
      setFiles(data)
    } catch (err) {
      setFilesError(err instanceof Error ? err.message : String(err))
    } finally {
      setIsLoadingFiles(false)
    }
  }, [token, handleLogout])

  useEffect(() => {
    if (token) {
      fetchFiles()
    }
  }, [token, fetchFiles])

  const handleDeleteFile = async (id) => {
    if (!confirm('Are you sure you want to delete this file?')) return;
    setDeletingId(id)
    try {
      const res = await fetch(`/files/${id}`, {
        method: 'DELETE',
        headers: { Authorization: `Bearer ${token}` }
      })
      if (res.status === 401) {
        handleLogout()
        return
      }
      if (!res.ok) throw new Error('Delete failed.')
      
      // If deleted file was being viewed, clear it
      if (selectedFile?.id === id) {
        setSelectedFile(null)
      }
      
      await fetchFiles()
    } catch (err) {
      alert(err instanceof Error ? err.message : 'Delete failed.')
    } finally {
      setDeletingId(null)
    }
  }

  const handleReadFile = (file) => {
    setSelectedFile(file)
  }

  const handleOpenFull = async (file) => {
    try {
      const res = await fetch(`/files/${file.id}/content`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      if (!res.ok) throw new Error('Failed to fetch file content.');
      const blob = await res.blob();
      const url = URL.createObjectURL(blob);
      window.open(url, '_blank');
    } catch (err) {
      alert(err instanceof Error ? err.message : 'Failed to open file.');
    }
  }

  // If no valid token, show login form
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
      if (fileInputRef.current) fileInputRef.current.value = ''
      return
    }

    const MAX_FILE_SIZE = 100 * 1024 * 1024 // 100MB
    if (file.size > MAX_FILE_SIZE) {
      setUploadError('The file is too large. Maximum size is 100MB.')
      if (fileInputRef.current) fileInputRef.current.value = ''
      return
    }

    setUploading(true)
    setUploadProgress(0)
    setUploadError('')
    setUploadMessage('')

    const formData = new FormData()
    formData.append('file', file)

    const xhr = new XMLHttpRequest()

    xhr.upload.addEventListener('progress', (event) => {
      if (event.lengthComputable) {
        const progress = Math.round((event.loaded / event.total) * 100)
        setUploadProgress(progress)
      }
    })

    xhr.onreadystatechange = () => {
      if (xhr.readyState === XMLHttpRequest.DONE) {
        setUploading(false)
        if (fileInputRef.current) fileInputRef.current.value = ''

        if (xhr.status === 200) {
          try {
            const payload = JSON.parse(xhr.responseText)
            setUploadMessage(`Uploaded ${payload?.fileName ?? file.name}`)
            fetchFiles()
          } catch {
            setUploadMessage(`Uploaded ${file.name}`)
            fetchFiles()
          }
        } else if (xhr.status === 401) {
          handleLogout()
        } else if (xhr.status === 413) {
          setUploadError('The file is too large for the server to process.')
        } else {
          try {
            const payload = JSON.parse(xhr.responseText)
            setUploadError(payload?.message ?? 'Upload failed. Try again.')
          } catch {
            setUploadError('Upload failed. Try again.')
          }
        }
      }
    }

    xhr.open('POST', '/files/upload')
    xhr.setRequestHeader('Authorization', `Bearer ${token}`)
    xhr.send(formData)
  }


  return (
    <div className="flex h-dvh overflow-hidden bg-slate-950 text-slate-100">
      <aside className={`flex h-full flex-col overflow-hidden border-r border-white/5 bg-slate-900/80 p-6 shadow-[0_20px_60px_rgba(2,6,23,0.7)] backdrop-blur-lg transition-all duration-300 ${
        isExplorerCollapsed ? 'w-20' : 'w-80'
      }`}>
        <div className="flex items-center justify-between text-[0.65rem] uppercase tracking-[0.45em] text-slate-500">
          <span>{isExplorerCollapsed ? 'Ex' : 'Explorer'}</span>
          <Button
            variant="ghost"
            size="icon"
            className="h-8 w-8 text-slate-400 hover:bg-white/10 hover:text-white"
            onClick={() => setIsExplorerCollapsed((prev) => !prev)}
            title={isExplorerCollapsed ? 'Expand explorer' : 'Collapse explorer'}
          >
            {isExplorerCollapsed ? <PanelLeftOpen size={15} /> : <PanelLeftClose size={15} />}
          </Button>
        </div>
        {!isExplorerCollapsed ? <div className="mt-8 flex min-h-0 flex-1 flex-col gap-6">
          <div className="rounded-2xl border border-white/10 bg-white/5 p-5 shadow-inner">
            <p className="text-[0.65rem] uppercase tracking-[0.35em] text-slate-500">Upload</p>
            <p className="mt-1 text-sm text-slate-400">Add a single PDF and we store it securely.</p>
            <div className="mt-5 flex flex-col gap-3">
              <Button onClick={handleUploadClick} className="font-semibold" disabled={uploading}>
                {uploading ? (
                  <span className="flex items-center gap-2">
                    <Loader2 className="h-4 w-4 animate-spin" />
                    {uploadProgress}%
                  </span>
                ) : (
                  'Upload PDF'
                )}
              </Button>
              
              {uploading && (
                <div className="h-1.5 w-full overflow-hidden rounded-full bg-white/10">
                  <div 
                    className="h-full bg-emerald-500 transition-all duration-300 ease-out"
                    style={{ width: `${uploadProgress}%` }}
                  />
                </div>
              )}

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

          <div className="mt-6 flex min-h-0 flex-1 flex-col gap-4">
            <p className="text-[0.65rem] uppercase tracking-[0.35em] text-slate-500">Your PDFs</p>
            {isLoadingFiles ? (
              <div className="flex items-center gap-3 px-2 text-sm text-slate-500">
                <Loader2 className="h-4 w-4 animate-spin" />
                Loading your documents...
              </div>
            ) : filesError ? (
              <p className="mt-2 text-sm text-destructive-foreground">{filesError}</p>
            ) : files.length ? (
              <div className="min-h-0 flex-1 overflow-y-auto pr-1">
                <div className="grid gap-2.5">
                {files.map((f) => (
                  <div
                    key={f.id}
                    className={`group flex min-w-0 flex-col gap-2 overflow-hidden rounded-xl border p-3 transition-all hover:bg-white/[0.06] hover:shadow-[0_8px_30px_rgb(0,0,0,0.12)] ${
                      selectedFile?.id === f.id
                        ? 'border-emerald-500 bg-white/[0.06]'
                        : 'border-white/5 bg-white/[0.03] hover:border-emerald-500/30'
                    }`}
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-emerald-500/10 text-emerald-500 transition-colors group-hover:bg-emerald-500/20">
                        <FileText size={16} />
                      </div>
                      <div className="flex gap-1 opacity-0 transition-opacity group-hover:opacity-100">
                        <Button
                          size="icon"
                          variant="ghost"
                          className="h-7 w-7 text-slate-400 hover:bg-white/10 hover:text-white"
                          onClick={() => handleReadFile(f)}
                          title="Read"
                        >
                          <Eye size={14} />
                        </Button>
                        <Button
                          size="icon"
                          variant="ghost"
                          className="h-7 w-7 text-slate-400 hover:bg-destructive/10 hover:text-destructive"
                          onClick={() => handleDeleteFile(f.id)}
                          disabled={deletingId === f.id}
                          title="Delete"
                        >
                          {deletingId === f.id ? (
                            <Loader2 size={14} className="animate-spin" />
                          ) : (
                            <Trash2 size={14} />
                          )}
                        </Button>
                      </div>
                    </div>
                    <div className="min-w-0">
                      <h3 className="truncate text-sm font-medium text-slate-200 group-hover:text-white" title={f.fileName}>
                        {f.fileName}
                      </h3>
                      <div className="mt-1 flex items-center gap-1.5 text-[10px] text-slate-500">
                        <Clock size={10} />
                        <span>{new Date(f.uploadedAt).toLocaleDateString()}</span>
                      </div>
                    </div>
                    <div className="mt-0.5 flex gap-1.5">
                      <Button
                        size="sm"
                        className={`h-6 flex-1 px-2 text-[11px] font-medium ${
                          selectedFile?.id === f.id ? 'bg-emerald-600 hover:bg-emerald-700' : ''
                        }`}
                        onClick={() => handleReadFile(f)}
                      >
                        Read
                      </Button>
                      <Button
                        size="sm"
                        variant="secondary"
                        className="h-6 px-2 text-[11px] font-medium text-slate-300"
                        onClick={() => handleOpenFull(f)}
                      >
                        <ExternalLink size={12} />
                      </Button>
                    </div>
                  </div>
                ))}
                </div>
              </div>
            ) : (
              <div className="rounded-xl border border-dashed border-white/10 p-6 text-center">
                <p className="text-xs text-slate-500">No documents found.</p>
              </div>
            )}
          </div>
        </div> : null}
      </aside>
      <main className="flex-1 overflow-hidden bg-gradient-to-br from-slate-950 via-slate-900 to-slate-950">
        <div className="flex h-full min-h-0 flex-col lg:flex-row">
          <div className="min-h-0 flex-1 overflow-hidden">
            {!selectedFile ? (
              <div className="flex h-full items-center justify-center p-10">
                <div className="mx-auto flex max-w-2xl flex-col gap-6 rounded-[36px] border border-white/10 bg-slate-900/70 p-10 shadow-[0_40px_60px_rgba(2,6,23,0.85)] md:p-12">
                  <div className="flex flex-col gap-6 md:flex-row md:items-center md:justify-between">
                    <div>
                      <p className="text-xs uppercase tracking-[0.4em] text-slate-500" aria-live="polite">
                        Workspace
                      </p>
                      <h1 className="text-4xl font-semibold text-white">Welcome back, {displayName}</h1>
                      <p className="mt-2 text-sm text-slate-400">
                        Select a document from the explorer to begin reading, or upload a new one.
                      </p>
                    </div>
                    <Button variant="ghost" onClick={handleLogout} className="text-slate-200 hover:bg-white/10">
                      Sign out
                    </Button>
                  </div>
                  <p className="text-sm text-slate-400">
                    Everything you upload is archived in Azure Blob Storage while we keep the file metadata, ownership, and audit trail in MongoDB.
                  </p>
                </div>
              </div>
            ) : (
              <div className="flex h-full min-h-0 flex-col">
                <header className="flex h-16 shrink-0 items-center justify-between border-b border-white/5 bg-slate-900/50 px-8 backdrop-blur-md">
                  <div className="flex items-center gap-4">
                    <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-emerald-500/10 text-emerald-500">
                      <FileText size={18} />
                    </div>
                    <div>
                      <h2 className="text-sm font-semibold text-white">{selectedFile.fileName}</h2>
                      <p className="text-[10px] uppercase tracking-wider text-slate-500">Document Reader</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-3">
                    <Button
                      variant="ghost"
                      size="sm"
                      className="h-8 text-xs text-slate-400 hover:bg-white/5 hover:text-white"
                      onClick={() => setSelectedFile(null)}
                    >
                      Close
                    </Button>
                    <Button
                      variant="secondary"
                      size="sm"
                      className="h-8 text-xs font-medium"
                      onClick={() => handleOpenFull(selectedFile)}
                    >
                      <ExternalLink size={14} className="mr-2" />
                      Full Window
                    </Button>
                    <div className="ml-4 h-6 w-px bg-white/10" />
                    <Button variant="ghost" onClick={handleLogout} className="ml-2 text-xs text-slate-400 hover:text-white">
                      Sign out
                    </Button>
                  </div>
                </header>
                <div className="min-h-0 flex-1 overflow-hidden bg-slate-900/20">
                  <PdfViewer key={selectedFile.id} fileId={selectedFile.id} token={token} />
                </div>
              </div>
            )}
          </div>
          <DocumentChatPanel />
        </div>
      </main>
    </div>
  )
}

export default App
