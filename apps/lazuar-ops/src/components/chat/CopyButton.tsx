import { useState, useCallback, useRef } from 'react'
import { Copy, Check } from 'lucide-react'
import { cn } from '../../lib/utils'

interface CopyButtonProps {
  text: string
  variant?: 'message' | 'code'
}

export function CopyButton({ text, variant = 'message' }: CopyButtonProps) {
  const [copied, setCopied] = useState(false)
  const timeoutRef = useRef<ReturnType<typeof setTimeout>>(null)

  const handleCopy = useCallback(
    (e: React.MouseEvent) => {
      e.stopPropagation()
      if (timeoutRef.current) clearTimeout(timeoutRef.current)

      const onSuccess = () => {
        setCopied(true)
        timeoutRef.current = setTimeout(() => setCopied(false), 2000)
      }

      if (navigator.clipboard?.writeText) {
        navigator.clipboard.writeText(text).then(onSuccess, () => fallbackCopy(text, onSuccess))
      } else {
        fallbackCopy(text, onSuccess)
      }
    },
    [text],
  )

  if (variant === 'code') {
    return (
      <button
        className={cn(
          'inline-flex items-center gap-1 rounded-sm px-1.5 py-0.5 font-mono text-[11px] transition-opacity',
          copied
            ? 'text-emerald-600'
            : 'text-zinc-500 opacity-0 hover:text-zinc-700 group-hover:opacity-100',
        )}
        onClick={handleCopy}
        title={copied ? 'Copied!' : 'Copy code'}
      >
        {copied ? <Check className="h-3 w-3" /> : <Copy className="h-3 w-3" />}
        <span>{copied ? 'Copied' : 'Copy'}</span>
      </button>
    )
  }

  return (
    <button
      className={cn(
        'rounded-sm p-1.5 transition-colors',
        copied ? 'text-emerald-500' : 'text-muted-foreground hover:bg-accent hover:text-foreground',
      )}
      onClick={handleCopy}
      title={copied ? 'Copied!' : 'Copy'}
    >
      {copied ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />}
    </button>
  )
}

function fallbackCopy(text: string, onSuccess: () => void) {
  try {
    const textarea = document.createElement('textarea')
    textarea.value = text
    Object.assign(textarea.style, {
      position: 'fixed',
      left: '-9999px',
      top: '-9999px',
      opacity: '0',
    })
    document.body.appendChild(textarea)
    textarea.select()
    document.execCommand('copy')
    document.body.removeChild(textarea)
    onSuccess()
  } catch {
    console.warn('Copy failed')
  }
}
