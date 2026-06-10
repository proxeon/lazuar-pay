import { useMemo } from 'react'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import remarkMath from 'remark-math'
import rehypeKatex from 'rehype-katex'
import rehypeRaw from 'rehype-raw'
import rehypeSanitize, { defaultSchema } from 'rehype-sanitize'
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter'
import type { Components } from 'react-markdown'
import { CopyButton } from './CopyButton'

const sanitizeSchema = {
  ...defaultSchema,
  tagNames: [...(defaultSchema.tagNames ?? []), 'details', 'summary'],
  attributes: {
    ...defaultSchema.attributes,
    details: ['open'],
    code: ['className'],
  },
}

interface MarkdownContentProps {
  content: string
}

function normalizeLatexDelimiters(content: string): string {
  const codeBlocks: string[] = []
  const blockMath: string[] = []
  const inlineMath: string[] = []

  let parsed = content.replace(/(`{3,})([\s\S]*?)(?:\1|$)/g, (match) => {
    codeBlocks.push(match)
    return `%%CODEBLOCK_${codeBlocks.length - 1}%%`
  })

  parsed = parsed.replace(/(`{1,2})([\s\S]*?)\1/g, (match) => {
    codeBlocks.push(match)
    return `%%CODEBLOCK_${codeBlocks.length - 1}%%`
  })

  parsed = parsed.replace(/\\\[([\s\S]*?)(?:\\\]|$)/g, (_, inner) => {
    blockMath.push(`$$${inner}$$`)
    return `%%BLOCKMATH_${blockMath.length - 1}%%`
  })

  parsed = parsed.replace(/\\\(([\s\S]*?)(?:\\\)|$)/g, (_, inner) => {
    inlineMath.push(`$${inner.trim()}$`)
    return `%%INLINEMATH_${inlineMath.length - 1}%%`
  })

  parsed = parsed.replace(/\$\$([\s\S]*?)(?:\$\$|$)/g, (_, inner) => {
    blockMath.push(`$$${inner}$$`)
    return `%%BLOCKMATH_${blockMath.length - 1}%%`
  })

  parsed = parsed.replace(/%%INLINEMATH_(\d+)%%/g, (_, idx) => inlineMath[Number(idx)])
  parsed = parsed.replace(/%%BLOCKMATH_(\d+)%%/g, (_, idx) => blockMath[Number(idx)])
  parsed = parsed.replace(/%%CODEBLOCK_(\d+)%%/g, (_, idx) => codeBlocks[Number(idx)])

  return parsed
}

function createComponents(): Components {
  return {
    pre({ children }) {
      return <>{children}</>
    },
    code({ className, children, ...rest }) {
      const match = /language-(\w+)/.exec(className || '')
      const raw = String(children)
      const codeString = raw.replace(/\n$/, '')

      if (match || raw.includes('\n')) {
        const language = match?.[1] ?? 'text'
        const showLabel = !!match

        return (
          <div className="group my-4 overflow-hidden rounded-md text-[13px] shadow-sm antialiased border border-[#e5e5e5]">
            <div className="flex items-center justify-between bg-[#f4f4f5] px-3 py-1.5 border-b border-[#e5e5e5]">
              <span className="font-mono text-[10px] font-bold uppercase tracking-widest text-[#71717a]">
                {showLabel ? language : ''}
              </span>
              <CopyButton text={codeString} variant="code" />
            </div>
            <SyntaxHighlighter
              useInlineStyles={false}
              language={language}
              PreTag="div"
              className="!m-0 !p-4 overflow-x-auto bg-[#fafafa] text-[13px]"
            >
              {codeString}
            </SyntaxHighlighter>
          </div>
        )
      }
      return (
        <code className="rounded-sm bg-[#f4f4f5] px-1.5 py-0.5 font-mono text-[0.875em] text-[#09090b] antialiased" {...rest}>
          {children}
        </code>
      )
    },
    a({ href, children, ...rest }) {
      return (
        <a href={href} target="_blank" rel="noopener noreferrer" className="text-blue-600 underline underline-offset-2 hover:opacity-80 font-medium" {...rest}>
          {children}
        </a>
      )
    },
    // ---- FIXED TABLE COMPONENTS ----
    table({ children, ...rest }) {
      return (
        // The "not-prose" class completely disables Tailwind Typography defaults for this block
        <div className="not-prose my-6 w-full overflow-hidden rounded-lg border border-[#e5e5e5] shadow-[0_2px_8px_rgba(0,0,0,0.04)] bg-white">
          <div className="w-full overflow-x-auto">
            <table className="w-full border-collapse text-left text-sm" {...rest}>
              {children}
            </table>
          </div>
        </div>
      )
    },
    thead({ children, ...rest }) {
      return (
        <thead className="bg-[#fafafa] border-b border-[#e5e5e5]" {...rest}>
          {children}
        </thead>
      )
    },
    tbody({ children, ...rest }) {
      return (
        <tbody className="divide-y divide-[#f4f4f5] bg-white" {...rest}>
          {children}
        </tbody>
      )
    },
    tr({ children, ...rest }) {
      return (
        <tr className="transition-colors hover:bg-[#fafafa]/50" {...rest}>
          {children}
        </tr>
      )
    },
    th({ children, ...rest }) {
      return (
        <th className="px-4 py-3 text-[11px] font-bold uppercase tracking-widest text-[#71717a] whitespace-nowrap" {...rest}>
          {children}
        </th>
      )
    },
    td({ children, ...rest }) {
      return (
        <td className="px-4 py-3.5 text-[13px] text-[#09090b] font-medium align-middle" {...rest}>
          {children}
        </td>
      )
    },
    // ---------------------------------
    blockquote({ children, ...rest }) {
      return (
        <blockquote className="my-4 border-l-2 border-[#a1a1aa] pl-4 italic text-[#71717a]" {...rest}>
          {children}
        </blockquote>
      )
    },
    hr({ ...rest }) {
      return <hr className="my-6 border-[#e5e5e5]" {...rest} />
    }
  }
}

export function MarkdownContent({ content }: MarkdownContentProps) {
  const components = useMemo(() => createComponents(), [])
  const normalizedContent = useMemo(() => normalizeLatexDelimiters(content), [content])

  return (
    <div className="prose prose-sm max-w-none w-full min-w-0 break-words antialiased
      prose-p:leading-relaxed prose-p:text-[#09090b]
      prose-pre:my-0 prose-pre:bg-transparent prose-pre:p-0 prose-pre:shadow-none 
      prose-img:rounded-md 
      prose-headings:font-semibold prose-headings:tracking-tight prose-headings:text-[#09090b]
      prose-h1:text-lg prose-h1:mt-5 prose-h1:mb-2.5 
      prose-h2:text-base prose-h2:mt-4 prose-h2:mb-2 
      prose-h3:text-sm prose-h3:mt-3 prose-h3:mb-1.5
      prose-strong:font-semibold prose-strong:text-[#09090b]
      prose-ul:my-2 prose-li:my-0.5"
    >
      <ReactMarkdown
        remarkPlugins={[remarkGfm, remarkMath]}
        rehypePlugins={[
          rehypeRaw,                                    
          [rehypeSanitize, sanitizeSchema],             
          rehypeKatex,                                  
        ]}
        components={components}
      >
        {normalizedContent}
      </ReactMarkdown>
    </div>
  )
}
