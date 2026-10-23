export interface Feedback {
  message: string;
  correlationId: string;
  errors: { code: string; message?: string; field?: string }[];
  kind: 'error' | 'warning' | 'success';
  conflict?: boolean;
  temporary?: boolean;
}
