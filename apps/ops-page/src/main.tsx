import {StrictMode} from 'react';
import {createRoot} from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter } from 'react-router-dom';
import { Toaster } from 'sonner';
import App from './App.tsx';
import './index.css';

const queryClient = new QueryClient();

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <App />
        {/* Rendered above all standard overlays to ensure backend errors are always visible */}
        <Toaster position="top-center" richColors theme="light" style={{ zIndex: 9999 }} />
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
);
