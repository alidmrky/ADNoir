import { afterEach, beforeEach, expect, it } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import type { ReactNode } from 'react';
import { server } from '../mocks/server';
import { success, failure } from '../mocks/envelopes';
import { configureApplication } from '../src/app/bootstrap';
import { queryClient } from '../src/lib/query/client';
import { setLanguage } from '../src/lib/i18n';
import { TenantsPage } from '../src/features/tenants';
import { RolesPage } from '../src/features/roles';
import { NotificationsPage } from '../src/features/notifications';
import { AuthPage } from '../src/features/auth';
const base = 'http://localhost:5070';
const tenant = {
  id: 'tenant-id',
  name: 'First tenant',
  slug: 'first',
  status: 'active',
  hierarchyEnabled: true,
  timeZoneId: 'UTC',
  permissionVersion: 1,
  createdAtUtc: '2026-01-01T00:00:00Z',
  version: 3,
};
let cleanup: () => void;
beforeEach(() => {
  cleanup = configureApplication();
  queryClient.clear();
  setLanguage('en');
});
afterEach(() => {
  cleanup();
  queryClient.clear();
});
function mount(element: ReactNode) {
  return render(<QueryClientProvider client={queryClient}>{element}</QueryClientProvider>);
}

it('uses server pagination and refetches the selected page', async () => {
  const pages: string[] = [];
  server.use(
    http.get(`${base}/api/v1/tenants/`, ({ request }) => {
      const page = new URL(request.url).searchParams.get('pageNumber')!;
      pages.push(page);
      return HttpResponse.json(
        success([tenant], {
          pageNumber: Number(page),
          pageSize: 20,
          totalCount: 41,
          totalPages: 3,
        }),
      );
    }),
  );
  mount(<TenantsPage />);
  expect(await screen.findByText('First tenant')).toBeTruthy();
  await userEvent.click(screen.getByRole('button', { name: 'Next' }));
  await screen.findByText('Page 2 of 3 · 41 records');
  expect(pages).toEqual(['1', '2']);
});

it('reads current version, stops on conflict, and requires explicit reload before saving again', async () => {
  let detailReads = 0;
  const bodies: unknown[] = [];
  server.use(
    http.get(`${base}/api/v1/tenants/`, () =>
      HttpResponse.json(
        success([tenant], { pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1 }),
      ),
    ),
    http.get(`${base}/api/v1/tenants/tenant-id`, () => {
      detailReads++;
      return HttpResponse.json(
        success({
          ...tenant,
          name: detailReads === 1 ? 'Current tenant' : 'Latest tenant',
          version: detailReads === 1 ? 5 : 9,
        }),
      );
    }),
    http.put(`${base}/api/v1/tenants/tenant-id`, async ({ request }) => {
      bodies.push(await request.json());
      return HttpResponse.json(
        bodies.length === 1 ? failure('concurrencyConflict') : success(tenant),
        { status: bodies.length === 1 ? 409 : 200 },
      );
    }),
  );
  mount(<TenantsPage />);
  await screen.findByText('First tenant');
  await userEvent.click(screen.getByRole('button', { name: 'Edit' }));
  const name = await screen.findByLabelText('Name');
  await userEvent.clear(name);
  await userEvent.type(name, 'My change');
  await userEvent.click(screen.getByRole('button', { name: 'Save' }));
  await screen.findByText(/Another operation changed/);
  expect(bodies).toEqual([{ name: 'My change', timeZoneId: 'UTC', status: 1, expectedVersion: 5 }]);
  expect((screen.getByRole('button', { name: 'Save' }) as HTMLButtonElement).disabled).toBe(true);
  await userEvent.click(screen.getByRole('button', { name: 'Open latest record' }));
  await screen.findByDisplayValue('Latest tenant');
  await userEvent.click(screen.getByRole('button', { name: 'Save' }));
  await waitFor(() => expect(bodies).toHaveLength(2));
  expect(bodies[1]).toEqual({
    name: 'Latest tenant',
    timeZoneId: 'UTC',
    status: 1,
    expectedVersion: 9,
  });
});

it('renders a permission denial and reference rather than hiding the feature based on roles', async () => {
  server.use(
    http.get(`${base}/api/v1/roles/`, () =>
      HttpResponse.json(failure('forbidden', 'auth.permission_denied'), { status: 403 }),
    ),
  );
  mount(<RolesPage />);
  expect(await screen.findByText('Sunucu hata açıklaması.')).toBeTruthy();
  expect(screen.getByText('server-reference')).toBeTruthy();
  expect(screen.getByRole('button', { name: 'New record' })).toBeTruthy();
});

it('shows a temporary permission matrix failure with retry', async () => {
  server.use(
    http.get(`${base}/api/v1/roles/`, () =>
      HttpResponse.json(failure('externalServiceError', 'auth.permission_matrix_unavailable'), {
        status: 502,
      }),
    ),
  );
  mount(<RolesPage />);
  expect(
    await screen.findByText(/Permission information is temporarily/, {}, { timeout: 3000 }),
  ).toBeTruthy();
  expect(screen.getByRole('button', { name: 'Try again' })).toBeTruthy();
});

it('keeps server validation visible at the form and explicitly mapped field', async () => {
  server.use(
    http.post(`${base}/api/v1/auth/login`, () =>
      HttpResponse.json(
        failure('validationFailed', 'common.validation_failed', {
          errors: [
            {
              code: 'validation.invalid_email',
              field: 'Email',
              message: 'Server validation detail.',
            },
          ],
        }),
        { status: 400 },
      ),
    ),
  );
  mount(<AuthPage />);
  await userEvent.type(screen.getByLabelText('Tenant slug'), 'example');
  await userEvent.type(screen.getByLabelText('Email'), 'test@example.com');
  await userEvent.type(screen.getByLabelText('Password'), 'password');
  await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));
  await screen.findByRole('alert');
  expect((await screen.findAllByText('Server validation detail.')).length).toBeGreaterThan(0);
  expect(screen.getByText('server-reference')).toBeTruthy();
});

it('displays bounded statistics and filters notification history by actual delivery status', async () => {
  const filters: string[] = [];
  server.use(
    http.get(`${base}/api/v1/notifications/emails/statistics`, () =>
      HttpResponse.json(
        success({
          fromUtc: '2026-01-01T00:00:00Z',
          toUtc: '2026-01-31T00:00:00Z',
          total: 12,
          byStatus: { accepted: 10 },
          attempts: 14,
          retriedMessages: 2,
        }),
      ),
    ),
    http.get(`${base}/api/v1/notifications/emails/`, ({ request }) => {
      filters.push(new URL(request.url).searchParams.get('status') || '');
      return HttpResponse.json(success([]));
    }),
  );
  mount(<NotificationsPage />);
  await screen.findByText('12');
  expect(screen.getByText(/SMTP acceptance does not mean delivery/)).toBeTruthy();
  await userEvent.selectOptions(screen.getByLabelText('Status'), 'outcomeUnknown');
  await waitFor(() => expect(filters).toEqual(['', 'outcomeUnknown']));
});
