import { memo, useEffect, useMemo, useRef, useState } from 'react';
import { Document, Page, pdfjs } from 'react-pdf';
import 'react-pdf/dist/Page/AnnotationLayer.css';
import 'react-pdf/dist/Page/TextLayer.css';
import { ZoomIn, ZoomOut, Loader2, FileText } from 'lucide-react';
import { Button } from '@/components/ui/button';

pdfjs.GlobalWorkerOptions.workerSrc = new URL(
  'pdfjs-dist/build/pdf.worker.min.mjs',
  import.meta.url,
).toString();

const LazyPdfPage = memo(function LazyPdfPage({ pageNumber, scale, scrollRootRef }) {
  const wrapperRef = useRef(null);
  const [shouldRender, setShouldRender] = useState(pageNumber <= 2);

  useEffect(() => {
    if (shouldRender) {
      return undefined;
    }

    const node = wrapperRef.current;
    if (!node) {
      return undefined;
    }

    const observer = new IntersectionObserver(
      (entries) => {
        const [entry] = entries;
        if (entry?.isIntersecting) {
          setShouldRender(true);
          observer.disconnect();
        }
      },
      {
        root: scrollRootRef.current,
        rootMargin: '900px 0px',
        threshold: 0.01,
      },
    );

    observer.observe(node);

    return () => {
      observer.disconnect();
    };
  }, [shouldRender, scrollRootRef]);

  return (
    <div ref={wrapperRef} className="w-full max-w-fit self-center">
      {shouldRender ? (
        <Page
          pageNumber={pageNumber}
          scale={scale}
          className="shadow-[0_20px_60px_rgba(0,0,0,0.5)] transition-all duration-300 ease-in-out hover:shadow-[0_30px_80px_rgba(0,0,0,0.6)]"
          renderAnnotationLayer
          renderTextLayer
          loading={
            <div className="h-[800px] w-[600px] rounded-2xl bg-slate-900/40 animate-pulse ring-1 ring-white/5" />
          }
        />
      ) : (
        <div
          className="w-[600px] rounded-2xl bg-slate-900/40 ring-1 ring-white/5"
          style={{ height: `${Math.max(500, Math.round(800 * scale))}px` }}
        />
      )}
    </div>
  );
});

export default function PdfViewer({ fileId, token }) {
  const [numPages, setNumPages] = useState(null);
  const [scale, setScale] = useState(1.0);
  const [retryKey, setRetryKey] = useState(0);
  const [loadFailed, setLoadFailed] = useState(false);
  const scrollRootRef = useRef(null);

  const fileSource = useMemo(
    () => ({
      url: `/files/${fileId}/content`,
      httpHeaders: { Authorization: `Bearer ${token}` },
    }),
    [fileId, token],
  );

  function onDocumentLoadSuccess({ numPages }) {
    setNumPages(numPages);
    setLoadFailed(false);
  }

  function onDocumentLoadError() {
    setLoadFailed(true);
  }

  function handleZoomIn() {
    setScale(prev => Math.min(3, prev + 0.1));
  }

  function handleZoomOut() {
    setScale(prev => Math.max(0.5, prev - 0.1));
  }

  function handleRetry() {
    setLoadFailed(false);
    setRetryKey((prev) => prev + 1);
  }

  return (
    <div className="flex h-full min-h-0 flex-col items-center bg-slate-900/40 p-6">
      {/* Viewer Toolbar */}
      <div className="sticky top-0 z-20 mb-6 flex shrink-0 items-center justify-between gap-6 rounded-2xl border border-white/5 bg-slate-900/80 p-3 shadow-2xl backdrop-blur-xl">
        <div className="flex items-center gap-3 px-2">
          <FileText size={18} className="text-emerald-500" />
          <div className="flex flex-col">
            <span className="text-[10px] text-slate-500 uppercase tracking-wider font-bold">
              Total Pages
            </span>
            <span className="text-xs font-semibold text-slate-200">
              {numPages || '--'} Pages
            </span>
          </div>
        </div>

        <span className="text-[11px] font-medium text-slate-400">Scroll to navigate pages</span>

        <div className="h-6 w-px bg-white/10" />

        <div className="flex items-center gap-2">
          <Button
            variant="ghost"
            size="icon"
            onClick={handleZoomOut}
            className="h-9 w-9 text-slate-400 hover:bg-white/5 hover:text-white"
            disabled={scale <= 0.5}
          >
            <ZoomOut size={18} />
          </Button>
          <span className="min-w-[3rem] text-center text-[11px] font-mono font-medium text-slate-400">
            {Math.round(scale * 100)}%
          </span>
          <Button
            variant="ghost"
            size="icon"
            onClick={handleZoomIn}
            className="h-9 w-9 text-slate-400 hover:bg-white/5 hover:text-white"
            disabled={scale >= 3}
          >
            <ZoomIn size={18} />
          </Button>
        </div>
      </div>

      {/* PDF Canvas area */}
      <div ref={scrollRootRef} className="w-full min-h-0 flex-1 overflow-auto rounded-3xl border border-white/5 bg-slate-950/40 p-10 shadow-inner ring-1 ring-white/10">
        <Document
          key={`${fileId}-${retryKey}`}
          file={fileSource}
          onLoadSuccess={onDocumentLoadSuccess}
          onLoadError={onDocumentLoadError}
          loading={
            <div className="flex h-[500px] w-full flex-col items-center justify-center gap-6">
              <div className="relative">
                <Loader2 className="h-12 w-12 animate-spin text-emerald-500" />
                <div className="absolute inset-0 h-12 w-12 blur-xl bg-emerald-500/20 animate-pulse" />
              </div>
              <div className="text-center">
                <p className="text-sm font-medium text-slate-300">Decrypting document...</p>
                <p className="mt-1 text-xs text-slate-600">This may take a moment for larger files.</p>
              </div>
            </div>
          }
          error={
            <div className="flex h-[400px] flex-col items-center justify-center p-8 text-center">
              <div className="h-16 w-16 rounded-2xl bg-destructive/10 flex items-center justify-center text-destructive mb-4">
                <FileText size={32} />
              </div>
              <h3 className="text-lg font-semibold text-white">Load failed</h3>
              <p className="mt-2 text-sm text-slate-400 max-w-xs">
                We couldn't load this PDF. The file might be corrupted or the link has expired.
              </p>
              <Button 
                variant="outline" 
                className="mt-6 border-white/10 text-slate-300 hover:bg-white/5"
                onClick={handleRetry}
              >
                Try Again
              </Button>
            </div>
          }
          className="flex flex-col items-center gap-8"
        >
          {!loadFailed && numPages
            ? Array.from(new Array(numPages), (_, index) => (
                <LazyPdfPage
                  key={`page_${index + 1}_scale_${scale}`}
                  pageNumber={index + 1}
                  scale={scale}
                  scrollRootRef={scrollRootRef}
                />
              ))
            : null}
        </Document>
      </div>
    </div>
  );
}
