import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { queryClient } from './services/queryClient'
import './index.css'
import AppRoutes from './routes/AppRoutes'
import AuthSessionBoundary from './features/auth/components/AuthSessionBoundary'
import RouteSeoMetadata from './components/RouteSeoMetadata'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <RouteSeoMetadata />
        <AuthSessionBoundary><AppRoutes /></AuthSessionBoundary>
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
)
