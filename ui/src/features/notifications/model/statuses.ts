import type { EmailDeliveryStatus } from '../../../contracts/generated/notification';
export const deliveryStatuses: EmailDeliveryStatus[] = [
  'pending',
  'processing',
  'accepted',
  'retryScheduled',
  'failed',
  'skipped',
  'outcomeUnknown',
];
