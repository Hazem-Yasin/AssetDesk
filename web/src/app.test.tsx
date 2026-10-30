import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AppRoutes } from './App';
import { AuthProvider } from './auth';
import type { Asset, User } from './types';

const admin: User = { id: 1, email: 'admin@assetdesk.local', displayName: 'Ada Admin', role: 'Admin' };
const employee: User = { id: 2, email: 'sara@assetdesk.local', displayName: 'Sara', role: 'Employee' };

const asset = (over: Partial<Asset> = {}): Asset => ({
  id: 10, tag: 'LT-001', name: 'ThinkPad X1', category: 'Laptop', serialNumber: 'SN1',
  purchaseDate: '2024-01-15', purchasePrice: 1500, notes: null, status: 'Available', version: 3,
  currentAssignment: null, ...over,
});

const reply = (body: unknown, status = 200) =>
  Promise.resolve(new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }));

type Handler = (url: string, init?: RequestInit) => Promise<Response> | undefined;
function mockApi(handler: Handler) {
  const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
    const res = handler(String(input), init);
    return res ?? reply({ title: `Unexpected ${String(input)}` }, 500);
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

function signIn(user: User) {
  localStorage.setItem('assetdesk.token', 'test-token');
  localStorage.setItem('assetdesk.user', JSON.stringify(user));
}

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <AuthProvider><AppRoutes /></AuthProvider>
    </MemoryRouter>,
  );
}

beforeEach(() => localStorage.clear());
afterEach(() => vi.unstubAllGlobals());

describe('routing guard', () => {
  it('sends anonymous users to the login page', () => {
    mockApi(() => undefined);
    renderAt('/assets');
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeInTheDocument();
  });

  it('keeps employees out of admin-only pages', async () => {
    signIn(employee);
    mockApi((url) => (url.startsWith('/api/assets/stats') ? reply({ totalAssets: 0, overdue: 0, totalValue: 0, byStatus: {}, byCategory: {} }) : undefined));
    renderAt('/assets/new');
    expect(await screen.findByRole('heading', { name: 'Dashboard' })).toBeInTheDocument();
  });
});

describe('login', () => {
  it('signs in and shows the dashboard', async () => {
    const fetchMock = mockApi((url) => {
      if (url === '/api/auth/login') return reply({ token: 'jwt', expiresAt: '2030-01-01T00:00:00Z', user: employee });
      if (url === '/api/assets/stats') return reply({ totalAssets: 7, overdue: 1, totalValue: 9000, byStatus: { Available: 7 }, byCategory: { Laptop: 7 } });
      return undefined;
    });
    renderAt('/login');
    const user = userEvent.setup();
    await user.type(screen.getByLabelText('Email'), 'sara@assetdesk.local');
    await user.type(screen.getByLabelText('Password'), 'Employee#123');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('heading', { name: 'Dashboard' })).toBeInTheDocument();
    expect(await screen.findByText('Total assets')).toBeInTheDocument();
    expect(screen.getByText('Total assets').nextElementSibling).toHaveTextContent('7');
    expect(localStorage.getItem('assetdesk.token')).toBe('jwt');
    expect(fetchMock).toHaveBeenCalledWith('/api/auth/login', expect.objectContaining({ method: 'POST' }));
  });

  it('shows the server error for bad credentials', async () => {
    mockApi((url) => (url === '/api/auth/login' ? reply({ title: 'Unauthorized', detail: 'Invalid email or password.' }, 401) : undefined));
    renderAt('/login');
    const user = userEvent.setup();
    await user.type(screen.getByLabelText('Email'), 'x@y.com');
    await user.type(screen.getByLabelText('Password'), 'nope');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password.');
  });
});

describe('asset list', () => {
  it('queries the API with the chosen filters and resets to page 1', async () => {
    signIn(employee);
    const fetchMock = mockApi((url) =>
      url.startsWith('/api/assets?') ? reply({ items: [asset()], total: 25, page: 1, pageSize: 10 }) : undefined);
    renderAt('/assets');
    const user = userEvent.setup();

    expect(await screen.findByText('ThinkPad X1')).toBeInTheDocument();
    expect(screen.getByText(/Page 1 of 3/)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Next' }));
    await waitFor(() => expect(fetchMock.mock.calls.some(([u]) => String(u).includes('page=2'))).toBe(true));

    await user.selectOptions(screen.getByLabelText('Status'), 'Retired');
    await waitFor(() => {
      const last = String(fetchMock.mock.calls.at(-1)![0]);
      expect(last).toContain('status=Retired');
      expect(last).toContain('page=1');
    });
  });

  it('only offers "New asset" to admins', async () => {
    mockApi((url) => (url.startsWith('/api/assets?') ? reply({ items: [], total: 0, page: 1, pageSize: 10 }) : undefined));
    signIn(employee);
    const { unmount } = renderAt('/assets');
    expect(await screen.findByText('No assets match your filters.')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'New asset' })).not.toBeInTheDocument();
    unmount();

    signIn(admin);
    renderAt('/assets');
    expect(await screen.findByRole('link', { name: 'New asset' })).toBeInTheDocument();
  });
});

describe('asset edit form', () => {
  it('explains a version conflict and offers to reload', async () => {
    signIn(admin);
    mockApi((url, init) => {
      if (url === '/api/assets/10' && init?.method === 'PUT') return reply({ title: 'Conflict', detail: 'stale' }, 409);
      if (url === '/api/assets/10') return reply({ asset: asset(), history: [], attachments: [] });
      return undefined;
    });
    renderAt('/assets/10/edit');
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: 'Save changes' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Someone else changed this asset');
    expect(screen.getByRole('button', { name: 'Reload the latest version' })).toBeInTheDocument();
  });
});

describe('asset detail', () => {
  it('lets an employee check out an available asset to themselves', async () => {
    signIn(employee);
    let state = asset();
    const fetchMock = mockApi((url, init) => {
      if (url === '/api/assets/10/checkout') {
        const body = JSON.parse(String(init!.body));
        expect(body.userId).toBe(employee.id);
        state = asset({ status: 'CheckedOut', currentAssignment: {
          id: 1, assetId: 10, assetTag: 'LT-001', assetName: 'ThinkPad X1', userId: 2, userName: 'Sara',
          checkedOutAt: '2026-01-01T00:00:00Z', dueAt: body.dueAt, returnedAt: null, isOverdue: false, notes: null } });
        return reply(state);
      }
      if (url === '/api/assets/10') return reply({ asset: state, history: [], attachments: [] });
      return undefined;
    });
    renderAt('/assets/10');
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: 'Check out to me' }));
    expect(await screen.findByRole('button', { name: 'Check in' })).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith('/api/assets/10/checkout', expect.anything());
  });
});
