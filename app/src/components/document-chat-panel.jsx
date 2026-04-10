import { Bot, ImageIcon, MessageSquare, Send, Sigma } from 'lucide-react'
import ReactMarkdown from 'react-markdown'
import rehypeKatex from 'rehype-katex'
import remarkMath from 'remark-math'

import { Button } from '@/components/ui/button'

const previewResponse = String.raw`Once enabled, this panel will answer questions grounded in the selected PDF and cite relevant passages.

Inline math works, for example the Gaussian integral $\int_0^\infty e^{-x^2}\,dx = \frac{\sqrt{\pi}}{2}$.

Display math also renders in chat:

$$
\hat{f}(\omega)=\int_{-\infty}^{\infty} f(x)e^{-i\omega x}\,dx
$$`

export default function DocumentChatPanel({ width = 384 }) {
  return (
    <aside
      className="flex h-full min-h-0 w-full shrink-0 flex-col border-t border-white/5 bg-slate-950/60 lg:w-[var(--chat-width)] lg:border-l lg:border-t-0"
      style={{ '--chat-width': `${width}px` }}
    >
      <header className="flex shrink-0 items-center justify-center border-b border-white/5 px-5 py-4">
        <h3 className="text-sm font-semibold text-white">Chat</h3>
      </header>

      <div className="px-5 pt-3">
        <span className="inline-flex rounded-full border border-amber-300/35 bg-amber-400/10 px-2.5 py-1 text-[10px] font-semibold uppercase tracking-wide text-amber-200">
          Inactive
        </span>
      </div>

      <div className="flex min-h-0 flex-1 flex-col gap-4 overflow-y-auto px-5 py-4">
        <div className="rounded-xl border border-white/8 bg-white/[0.03] px-4 py-3 text-xs text-slate-300">
          Chat input is disabled until the backend RAG pipeline is connected.
        </div>

        <div className="rounded-2xl border border-emerald-400/15 bg-emerald-500/5 p-4">
          <div className="mb-2 flex items-center gap-2 text-[11px] uppercase tracking-widest text-emerald-300/80">
            <Bot size={14} />
            Text Answer Preview
          </div>
          <div className="chat-markdown text-sm text-slate-200">
            <ReactMarkdown remarkPlugins={[remarkMath]} rehypePlugins={[rehypeKatex]}>
              {previewResponse}
            </ReactMarkdown>
          </div>
        </div>

        <div className="rounded-2xl border border-sky-300/15 bg-sky-400/5 p-4">
          <div className="mb-2 flex items-center gap-2 text-[11px] uppercase tracking-widest text-sky-200/90">
            <ImageIcon size={14} />
            Image Response Preview
          </div>
          <div className="flex h-28 items-center justify-center rounded-xl border border-dashed border-sky-200/25 bg-slate-900/60 text-xs text-slate-400">
            Figures and extracted images will render here.
          </div>
        </div>

        <div className="rounded-2xl border border-fuchsia-300/15 bg-fuchsia-400/5 p-4">
          <div className="mb-2 flex items-center gap-2 text-[11px] uppercase tracking-widest text-fuchsia-200/85">
            <Sigma size={14} />
            Math Expression Preview
          </div>
          <div className="rounded-lg bg-slate-900/80 px-3 py-2 text-xs text-slate-200">
            <ReactMarkdown remarkPlugins={[remarkMath]} rehypePlugins={[rehypeKatex]}>
              {String.raw`$$\int_0^\infty e^{-x^2}\,dx = \frac{\sqrt{\pi}}{2}$$`}
            </ReactMarkdown>
          </div>
        </div>
      </div>

      <div className="shrink-0 border-t border-white/5 px-5 py-4">
        <div className="mb-3 flex items-center gap-2 text-xs text-slate-500">
          <MessageSquare size={14} />
          Messaging will unlock after backend integration.
        </div>
        <div className="flex gap-2">
          <input
            type="text"
            className="h-10 flex-1 rounded-xl border border-white/10 bg-slate-900/70 px-3 text-sm text-slate-500 outline-none"
            placeholder="Chat is currently unavailable"
            disabled
          />
          <Button size="icon" disabled className="h-10 w-10">
            <Send size={15} />
          </Button>
        </div>
      </div>
    </aside>
  )
}
