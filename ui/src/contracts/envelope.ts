export interface Envelope {
  isSuccess: boolean;
  status: string;
  messageCode: string;
  message?: string;
  data?: unknown;
  errors: { code: string; message?: string; field?: string }[];
  warnings: { code: string; message?: string; field?: string }[];
  correlationId?: string;
  pageNumber?: number;
  pageSize?: number;
  totalCount?: number;
  totalPages?: number;
}
