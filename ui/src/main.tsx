import { createRoot } from 'react-dom/client';
import { QueryClientProvider } from '@tanstack/react-query';
import { App } from './app/app';
import { configureApplication } from './app/bootstrap';
import { ErrorBoundary } from './app/error-boundary';
import { queryClient } from './lib/query/client';
import './styles/global.css';

configureApplication();
createRoot(document.getElementById('root')!).render(
  <ErrorBoundary>
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>
  </ErrorBoundary>,
);
