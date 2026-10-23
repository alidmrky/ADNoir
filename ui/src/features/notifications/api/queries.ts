import { useQuery } from '@tanstack/react-query';
import type {
  EmailDeliveryDto,
  EmailStatisticsDto,
} from '../../../contracts/generated/notification';
import { request } from '../../../lib/http/client';
import { notificationKeys } from './keys';
export function useEmails(status: string, language: string) {
  return useQuery({
    queryKey: notificationKeys.list(status, language),
    queryFn: ({ signal }) =>
      request<EmailDeliveryDto[]>(
        `/api/v1/notifications/emails/${status ? `?status=${encodeURIComponent(status)}` : ''}`,
        { signal },
      ),
  });
}
export function useEmail(id: string, language: string) {
  return useQuery({
    queryKey: notificationKeys.detail(id, language),
    enabled: !!id,
    queryFn: ({ signal }) =>
      request<EmailDeliveryDto>(`/api/v1/notifications/emails/${encodeURIComponent(id)}`, {
        signal,
      }),
  });
}
export function useStatistics(language: string) {
  return useQuery({
    queryKey: notificationKeys.statistics(language),
    queryFn: ({ signal }) =>
      request<EmailStatisticsDto>('/api/v1/notifications/emails/statistics', { signal }),
  });
}
