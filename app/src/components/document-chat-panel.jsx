import { useState, useEffect, useCallback, useRef } from 'react'
import { AlertCircle, Bot, FileText, Link2, MessageSquare, Send, User, Plus, X, Trash2, ChevronDown } from 'lucide-react'
import ReactMarkdown from 'react-markdown'
import rehypeKatex from 'rehype-katex'
import remarkMath from 'remark-math'

import { Button } from '@/components/ui/button'

const GROQ_MODEL = 'openai/gpt-oss-20b'

const citationLabelToAnchor = (label) =>
  `source-${label.toLowerCase().replace(/\s+/g, '-')}`

const normalizeAssistantMarkdown = (text) =>
  text
    .replace(/\\\[((?:.|\n)*?)\\\]/g, (_, expr) => `\n$$\n${expr.trim()}\n$$\n`)
    .replace(/\\\(((?:.|\n)*?)\\\)/g, (_, expr) => `$${expr.trim()}$`)
    .replace(/\[(source\s+\d+)\]/gi, (_, label) => `[${label}](#${citationLabelToAnchor(label)})`)
    .replace(/\((source\s+\d+)\)/gi, (_, label) => `[${label}](#${citationLabelToAnchor(label)})`)

const extractCitedSourceIndexes = (markdown) => {
  const matches = markdown.matchAll(/\[source\s+(\d+)\]|\(source\s+(\d+)\)/gi)
  const indexes = new Set()
  for (const match of matches) {
    const index = Number(match[1] ?? match[2])
    if (Number.isFinite(index) && index > 0) {
      indexes.add(index)
    }
  }
  return indexes
}

const extractSources = (responseBody) => {
  const rawSources = Array.isArray(responseBody?.sources)
    ? responseBody.sources
    : Array.isArray(responseBody?.Sources)
      ? responseBody.Sources
      : null
  if (!rawSources) return []
  return rawSources
    .map((source) => ({
      index: Number(source?.index ?? source?.Index),
      fileId: typeof (source?.fileId ?? source?.FileId) === 'string' ? (source.fileId ?? source.FileId) : '',
      fileName: typeof (source?.fileName ?? source?.FileName) === 'string'
        ? (source.fileName ?? source.FileName)
        : '',
      pageNumber: Number(source?.pageNumber ?? source?.PageNumber) || 1,
      score: Number(source?.score ?? source?.Score) || 0,
    }))
    .filter((source) => Number.isFinite(source.index) && source.index > 0)
}

export default function DocumentChatPanel({
  width = 384,
  selectedFileName = '',
  selectedFileId = '',
  token = '',
  onOpenSource,
}) {
  const [threads, setThreads] = useState([])
  const [activeThreadId, setActiveThreadId] = useState(null)
  const [messages, setMessages] = useState([])
  const [input, setInput] = useState('')
  const [isSending, setIsSending] = useState(false)
  const [isLoadingThreads, setIsLoadingThreads] = useState(false)
  const [isLoadingMessages, setIsLoadingMessages] = useState(false)
  const [error, setError] = useState('')
  const [isThreadDropdownOpen, setIsThreadDropdownOpen] = useState(false)
  const messagesEndRef = useRef(null)
  const dropdownRef = useRef(null)

  const fetchThreads = useCallback(async () => {
    if (!token) return
    setIsLoadingThreads(true)
    try {
      const res = await fetch('/chat/threads', { headers: { Authorization: `Bearer ${token}` } })
      if (!res.ok) return
      const data = await res.json()
      setThreads(Array.isArray(data) ? data : [])
    } catch { /* noop */
    } finally {
      setIsLoadingThreads(false)
    }
  }, [token])

  const fetchMessages = useCallback(async (threadId) => {
    if (!token || !threadId) return
    setIsLoadingMessages(true)
    try {
      const res = await fetch(`/chat/threads/${threadId}/messages`, { headers: { Authorization: `Bearer ${token}` } })
      if (!res.ok) return
      const data = await res.json()
      const loaded = Array.isArray(data) ? data.map((m) => ({
        id: m.id ?? crypto.randomUUID(),
        role: m.role,
        content: m.role === 'assistant' ? normalizeAssistantMarkdown(m.content ?? '') : (m.content ?? ''),
        sources: m.sources ? extractSources({ sources: m.sources }) : [],
      })) : []
      setMessages(loaded)
    } catch { /* noop */
    } finally {
      setIsLoadingMessages(false)
    }
  }, [token])

  const createThread = useCallback(async () => {
    if (!token) return
    try {
      const res = await fetch('/chat/threads', {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}` }
      })
      if (!res.ok) return
      const thread = await res.json()
      setThreads((prev) => [thread, ...prev].slice(0, 5))
      setActiveThreadId(thread.id)
      setMessages([])
    } catch { /* noop */
    }
  }, [token])

  const deleteThread = useCallback(async (threadId, event) => {
    event?.stopPropagation()
    if (!token) return
    try {
      const res = await fetch(`/chat/threads/${threadId}`, {
        method: 'DELETE',
        headers: { Authorization: `Bearer ${token}` }
      })
      if (res.ok) {
        setThreads((prev) => prev.filter((t) => t.id !== threadId))
        if (activeThreadId === threadId) {
          const remaining = threads.filter((t) => t.id !== threadId)
          if (remaining.length > 0) {
            setActiveThreadId(remaining[0].id)
            fetchMessages(remaining[0].id)
          } else {
            setActiveThreadId(null)
            setMessages([])
            createThread()
          }
        }
      }
    } catch { /* noop */
    }
  }, [token, activeThreadId, threads, fetchMessages, createThread])

  useEffect(() => {
    fetchThreads()
  }, [fetchThreads])

  useEffect(() => {
    if (activeThreadId) {
      fetchMessages(activeThreadId)
    }
  }, [activeThreadId, fetchMessages])

  useEffect(() => {
    if (messages.length > 0) {
      messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
    }
  }, [messages])

  useEffect(() => {
    const handleClickOutside = (event) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target)) {
        setIsThreadDropdownOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  const activeThread = threads.find((t) => t.id === activeThreadId)

  const handleSend = async () => {
    const prompt = input.trim()
    if (!prompt || isSending) return
    if (!token) {
      setError('You must be logged in to send chat messages.')
      return
    }

    setInput('')
    setError('')
    setIsSending(true)

    try {
      let threadId = activeThreadId
      if (!threadId) {
        const res = await fetch('/chat/threads', { method: 'POST', headers: { Authorization: `Bearer ${token}` } })
        if (!res.ok) throw new Error('Failed to create thread')
        const newThread = await res.json()
        threadId = newThread.id
        setThreads((prev) => [{ ...newThread, title: newThread.title }, ...prev].slice(0, 5))
        setActiveThreadId(threadId)
      }

      const response = await fetch('/chat/completions', {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({
          threadId,
          fileId: selectedFileId || undefined,
          messages: [
            {
              role: 'system',
              content: selectedFileName
                ? `You are a document assistant. The currently open file is "${selectedFileName}".`
                : 'You are a helpful assistant for PDF-related questions.',
            },
            ...messages.map((message) => ({ role: message.role, content: message.content })),
            { role: 'user', content: prompt },
          ],
        }),
      })

      const payload = await response.json().catch(() => null)
      if (!response.ok) {
        const apiError = payload?.error?.message || payload?.detail || 'Groq request failed.'
        throw new Error(apiError)
      }

      const persisted = Array.isArray(payload?.persistedMessages)
        ? payload.persistedMessages.map((m) => ({
            id: m.id ?? crypto.randomUUID(),
            role: m.role,
            content: m.role === 'assistant' ? normalizeAssistantMarkdown(m.content ?? '') : (m.content ?? ''),
            sources: m.sources ? extractSources({ sources: m.sources }) : [],
          }))
        : []

      const newMessages = [...messages, ...persisted]

      setMessages(newMessages)

      if (payload.threadId) {
        setThreads((prev) =>
          prev.map((t) =>
            t.id === payload.threadId ? { ...t, title: persisted[0]?.content ? (persisted[0].content.length > 60 ? persisted[0].content.slice(0, 60) + '...' : persisted[0].content) : t.title } : t
          )
        )
        fetchThreads()
      }
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Request failed.')
    } finally {
      setIsSending(false)
    }
  }

  return (
    <aside
      className="flex h-full min-h-0 w-full shrink-0 flex-col border-t border-white/5 bg-slate-950/60 lg:w-[var(--chat-width)] lg:border-l lg:border-t-0"
      style={{ '--chat-width': `${width}px` }}
    >
      <header className="flex shrink-0 items-center justify-between border-b border-white/5 px-4 py-3">
        <div className="relative" ref={dropdownRef}>
          <button
            type="button"
            className="flex max-w-[180px] items-center gap-2 rounded-lg px-2 py-1.5 text-sm font-semibold text-white hover:bg-white/10"
            onClick={() => setIsThreadDropdownOpen((prev) => !prev)}
          >
            <span className="truncate">
              {activeThread?.title ?? 'Select chat'}
            </span>
            <ChevronDown size={14} className={`shrink-0 transition-transform ${isThreadDropdownOpen ? 'rotate-180' : ''}`} />
          </button>
          {isThreadDropdownOpen && (
            <div className="absolute left-0 top-full z-50 mt-1 w-64 overflow-hidden rounded-xl border border-white/10 bg-slate-900 shadow-xl">
              <div className="flex items-center justify-between border-b border-white/5 px-3 py-2">
                <span className="text-xs font-semibold uppercase tracking-widest text-slate-400">Chat threads</span>
                <button
                  type="button"
                  className="flex items-center gap-1 rounded-md px-2 py-1 text-xs text-emerald-400 hover:bg-emerald-500/10"
                  onClick={() => { createThread(); setIsThreadDropdownOpen(false) }}
                >
                  <Plus size={12} />
                  New
                </button>
              </div>
              <div className="max-h-64 overflow-y-auto py-1">
                {isLoadingThreads ? (
                  <p className="px-3 py-2 text-xs text-slate-500">Loading...</p>
                ) : threads.length === 0 ? (
                  <p className="px-3 py-2 text-xs text-slate-500">No threads yet</p>
                ) : (
                  threads.map((thread) => (
                    <div
                      key={thread.id}
                      className={`group flex items-center gap-2 px-3 py-2 hover:bg-white/5 ${activeThreadId === thread.id ? 'bg-white/5' : ''}`}
                    >
                      <button
                        type="button"
                        className="flex min-w-0 flex-1 items-center gap-2 text-left"
                        onClick={() => { setActiveThreadId(thread.id); setIsThreadDropdownOpen(false) }}
                      >
                        <MessageSquare size={13} className="shrink-0 text-slate-400" />
                        <span className="truncate text-xs text-slate-200">{thread.title}</span>
                      </button>
                      <button
                        type="button"
                        className="shrink-0 rounded-md p-1 text-slate-500 opacity-0 hover:bg-red-500/10 hover:text-red-400 group-hover:opacity-100"
                        onClick={(e) => deleteThread(thread.id, e)}
                        title="Delete thread"
                      >
                        <Trash2 size={12} />
                      </button>
                    </div>
                  ))
                )}
              </div>
            </div>
          )}
        </div>
        <button
          type="button"
          className="flex shrink-0 items-center gap-1 rounded-lg p-1.5 text-slate-400 hover:bg-white/10 hover:text-white"
          onClick={createThread}
          title="New chat thread"
        >
          <Plus size={16} />
        </button>
      </header>

      <div className="flex min-h-0 flex-1 flex-col gap-4 overflow-y-auto px-5 py-4">
        {isLoadingMessages ? (
          <div className="flex items-center gap-2 text-xs text-slate-500">
            <div className="h-4 w-4 animate-spin rounded-full border-2 border-slate-500 border-t-transparent" />
            Loading messages...
          </div>
        ) : messages.length === 0 ? (
          <div className="flex flex-col items-center justify-center gap-3 rounded-xl border border-white/8 bg-white/[0.03] px-4 py-10 text-center">
            <MessageSquare size={24} className="text-slate-600" />
            <div>
              <p className="text-xs font-semibold uppercase tracking-widest text-slate-400">Start a conversation</p>
              <p className="mt-1 text-[11px] text-slate-500">
                Chat requests are routed through Groq ({GROQ_MODEL}).
                {selectedFileName ? ` Open file: ${selectedFileName}` : ''}
              </p>
            </div>
          </div>
        ) : (
          messages.map((message) => {
            const citedSourceIndexes = extractCitedSourceIndexes(message.content ?? '')
            const visibleSources = Array.isArray(message.sources)
              ? message.sources.filter((source) => citedSourceIndexes.has(source.index))
              : []
            return (
              <div
                key={message.id}
                className={`rounded-2xl border p-4 ${
                  message.role === 'assistant'
                    ? 'border-emerald-400/15 bg-emerald-500/5'
                    : 'border-sky-300/15 bg-sky-400/5'
                }`}
              >
                <div
                  className={`mb-2 flex items-center gap-2 text-[11px] uppercase tracking-widest ${
                    message.role === 'assistant' ? 'text-emerald-300/80' : 'text-sky-200/90'
                  }`}
                >
                  {message.role === 'assistant' ? <Bot size={14} /> : <User size={14} />}
                  {message.role === 'assistant' ? 'Assistant' : 'You'}
                </div>
                {message.role === 'assistant' ? (
                  <div className="chat-markdown text-sm text-slate-200">
                    <ReactMarkdown
                      remarkPlugins={[remarkMath]}
                      rehypePlugins={[rehypeKatex]}
                      components={{
                        a: ({ href, children, ...props }) => {
                          if (!href) return <span>{children}</span>
                          if (href.startsWith('#source-')) {
                            const sourceIndex = Number(href.split('-').pop())
                            const source = Array.isArray(message.sources)
                              ? message.sources.find((item) => item.index === sourceIndex)
                              : null
                            return (
                              <button
                                type="button"
                                className="inline-flex items-center gap-1 rounded-md border border-emerald-300/30 bg-emerald-500/10 px-1.5 py-0.5 text-xs font-semibold text-emerald-100 hover:bg-emerald-400/20"
                                onClick={() => { if (source) onOpenSource?.(source) }}
                              >
                                <Link2 size={12} />
                                {children}
                              </button>
                            )
                          }
                          return (
                            <a {...props} href={href} target="_blank" rel="noreferrer noopener">
                              {children}
                            </a>
                          )
                        },
                      }}
                    >
                      {message.content}
                    </ReactMarkdown>
                    {visibleSources.length > 0 && (
                      <div className="chat-sources mt-4 rounded-xl border border-white/10 bg-white/[0.02] p-3">
                        <p className="mb-2 text-[10px] font-semibold uppercase tracking-[0.2em] text-slate-400">Sources</p>
                        <div className="space-y-2">
                          {visibleSources.map((source) => (
                            <button
                              key={`${message.id}-source-${source.index}`}
                              id={`source-${source.index}`}
                              type="button"
                              className="chat-source-button flex w-full items-center justify-between rounded-lg border border-white/10 bg-slate-900/50 px-3 py-2 text-left"
                              onClick={() => onOpenSource?.(source)}
                            >
                              <span className="flex min-w-0 items-center gap-2">
                                <FileText size={13} className="shrink-0 text-emerald-300/80" />
                                <span className="truncate text-xs text-slate-200">
                                  source {source.index}: {source.fileName}
                                </span>
                              </span>
                              <span className="ml-3 shrink-0 text-[11px] text-slate-400">
                                page {source.pageNumber} · {source.score.toFixed(3)}
                              </span>
                            </button>
                          ))}
                        </div>
                      </div>
                    )}
                  </div>
                ) : (
                  <p className="text-sm text-slate-200">{message.content}</p>
                )}
              </div>
            )
          })
        )}
        <div ref={messagesEndRef} />
        {error && (
          <div className="flex items-start gap-2 rounded-xl border border-red-300/20 bg-red-500/10 px-3 py-2 text-xs text-red-200">
            <AlertCircle size={14} className="mt-0.5 shrink-0" />
            <span>{error}</span>
          </div>
        )}
      </div>

      <div className="shrink-0 border-t border-white/5 px-5 py-4">
        <div className="mb-3 flex items-center gap-2 text-xs text-slate-500">
          <MessageSquare size={14} />
          {isSending ? 'Waiting for Groq response...' : 'Type a message and press Enter to send.'}
        </div>
        <div className="flex gap-2">
          <input
            type="text"
            className="h-10 flex-1 rounded-xl border border-white/10 bg-slate-900/70 px-3 text-sm text-slate-100 outline-none"
            placeholder="Ask about your document..."
            value={input}
            onChange={(event) => setInput(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter') { event.preventDefault(); handleSend() }
            }}
            disabled={isSending}
          />
          <Button size="icon" className="h-10 w-10" onClick={handleSend} disabled={isSending || !input.trim()}>
            <Send size={15} />
          </Button>
        </div>
      </div>
    </aside>
  )
}
