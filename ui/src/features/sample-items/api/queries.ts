import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import type { SampleItemDto, CreateSampleItemRequest } from '../../../contracts/generated/sample';
import { request } from '../../../lib/http/client';
import { sampleKeys } from './keys';
export function useSample(id: string, language: string) {
  return useQuery({
    queryKey: sampleKeys.detail(id, language),
    enabled: !!id,
    queryFn: ({ signal }) =>
      request<SampleItemDto>(`/api/v1/sample-items/${encodeURIComponent(id)}`, { signal }),
  });
}
export function useCreateSample(language: string, onCreated: (id: string) => void) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateSampleItemRequest) =>
      request<SampleItemDto>('/api/v1/sample-items/', { method: 'POST', body }),
    onSuccess: (record) => {
      client.setQueryData(sampleKeys.detail(record.id, language), record);
      onCreated(record.id);
    },
  });
}
