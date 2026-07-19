import React from 'react'
import ReactDOM from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import App from './App'

// i18n configuration
import './i18n'

// Self-hosted fonts (bao gồm subset vietnamese) — đảm bảo render nhất quán trên mọi OS
import '@fontsource/inter/400.css'
import '@fontsource/inter/500.css'
import '@fontsource/inter/600.css'
import '@fontsource/inter/700.css'
import '@fontsource/inter/800.css'
import '@fontsource/plus-jakarta-sans/500.css'
import '@fontsource/plus-jakarta-sans/600.css'
import '@fontsource/plus-jakarta-sans/700.css'
import '@fontsource/plus-jakarta-sans/800.css'

import './index.css'

// Shim `process` cho thư viện đọc `process.env.NODE_ENV` ở runtime
// (react-grid-layout/react-draggable) — tránh `ReferenceError: process is not defined`.
if (typeof (globalThis as unknown as { process?: unknown }).process === 'undefined') {
  ;(globalThis as unknown as { process: { env: Record<string, string> } }).process = {
    env: { NODE_ENV: import.meta.env.MODE },
  }
}

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 1000 * 60 * 5,
      retry: 1,
    },
  },
})

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
        <App />
      </BrowserRouter>
    </QueryClientProvider>
  </React.StrictMode>
)
