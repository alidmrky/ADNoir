import { Component, type ReactNode } from 'react';
import { t } from '../lib/i18n';
import { FeedbackBox } from '../components/common/feedback';
import { Button } from '../components/ui/button';
export class ErrorBoundary extends Component<
  { children: ReactNode },
  { failed: boolean; reference: string }
> {
  state = { failed: false, reference: crypto.randomUUID() };
  static getDerivedStateFromError() {
    return { failed: true };
  }
  render() {
    if (this.state.failed)
      return (
        <main className="main-content">
          <h1>{t('errorTitle')}</h1>
          <FeedbackBox
            value={{
              kind: 'error',
              message: t('renderError'),
              correlationId: this.state.reference,
              errors: [],
            }}
          />
          <Button onClick={() => window.location.reload()}>{t('retry')}</Button>
        </main>
      );
    return this.props.children;
  }
}
