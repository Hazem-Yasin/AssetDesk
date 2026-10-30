import type {
  Asset, AssetDetail, AssetInput, AuthResponse, Assignment, Attachment, Paged, Stats, User,
} from './types';

const TOKEN_KEY = 'assetdesk.token';

export const tokenStore = {
  get: () => localStorage.getItem(TOKEN_KEY),
  set: (token: string) => localStorage.setItem(TOKEN_KEY, token),
  clear: () => localStorage.removeItem(TOKEN_KEY),
};

/** Error carrying the HTTP status and the server's problem-details message. */
export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
  }
}

let onUnauthorized: () => void = () => {};
export const setUnauthorizedHandler = (handler: () => void) => { onUnauthorized = handler; };

async function readError(res: Response): Promise<string> {
  try {
    const body = await res.json();
    if (body?.errors) return Object.values(body.errors as Record<string, string[]>).flat().join(' ');
    return body?.detail ?? body?.title ?? `Request failed (${res.status})`;
  } catch {
    return res.status === 429 ? 'Too many attempts. Please wait a minute.' : `Request failed (${res.status})`;
  }
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  const token = tokenStore.get();
  if (token) headers.set('Authorization', `Bearer ${token}`);
  if (init.body && !(init.body instanceof FormData)) headers.set('Content-Type', 'application/json');

  const res = await fetch(path, { ...init, headers });
  if (res.status === 401 && token) onUnauthorized();
  if (!res.ok) throw new ApiError(res.status, await readError(res));
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

const json = (method: string, body?: unknown): RequestInit => ({
  method,
  body: body === undefined ? undefined : JSON.stringify(body),
});

export interface AssetFilter {
  search?: string;
  status?: string;
  category?: string;
  page: number;
  pageSize: number;
}

export const api = {
  login: (email: string, password: string) =>
    request<AuthResponse>('/api/auth/login', json('POST', { email, password })),
  register: (email: string, displayName: string, password: string) =>
    request<AuthResponse>('/api/auth/register', json('POST', { email, displayName, password })),
  users: () => request<User[]>('/api/auth/users'),

  stats: () => request<Stats>('/api/assets/stats'),
  overdue: () => request<Assignment[]>('/api/assets/overdue'),
  mine: () => request<Asset[]>('/api/assets/mine'),
  search: (f: AssetFilter) => {
    const q = new URLSearchParams({ page: String(f.page), pageSize: String(f.pageSize) });
    if (f.search) q.set('search', f.search);
    if (f.status) q.set('status', f.status);
    if (f.category) q.set('category', f.category);
    return request<Paged<Asset>>(`/api/assets?${q}`);
  },
  get: (id: number) => request<AssetDetail>(`/api/assets/${id}`),
  create: (input: AssetInput) => request<Asset>('/api/assets', json('POST', input)),
  update: (id: number, input: Omit<AssetInput, 'tag'> & { version: number }) =>
    request<Asset>(`/api/assets/${id}`, json('PUT', input)),
  remove: (id: number) => request<void>(`/api/assets/${id}`, json('DELETE')),

  checkOut: (id: number, userId: number, dueAt: string, notes: string | null) =>
    request<Asset>(`/api/assets/${id}/checkout`, json('POST', { userId, dueAt, notes })),
  checkIn: (id: number) => request<Asset>(`/api/assets/${id}/checkin`, json('POST')),
  toMaintenance: (id: number) => request<Asset>(`/api/assets/${id}/maintenance`, json('POST')),
  fromMaintenance: (id: number) => request<Asset>(`/api/assets/${id}/maintenance/complete`, json('POST')),
  retire: (id: number) => request<Asset>(`/api/assets/${id}/retire`, json('POST')),

  upload: (id: number, file: File) => {
    const form = new FormData();
    form.append('file', file);
    return request<Attachment>(`/api/assets/${id}/attachments`, { method: 'POST', body: form });
  },
  deleteAttachment: (id: number, attachmentId: number) =>
    request<void>(`/api/assets/${id}/attachments/${attachmentId}`, json('DELETE')),

  /** Downloads need the bearer token, so fetch the blob and hand back an object URL. */
  async downloadAttachment(id: number, attachment: Attachment): Promise<void> {
    const res = await fetch(`/api/assets/${id}/attachments/${attachment.id}`, {
      headers: { Authorization: `Bearer ${tokenStore.get() ?? ''}` },
    });
    if (!res.ok) throw new ApiError(res.status, await readError(res));
    const url = URL.createObjectURL(await res.blob());
    const link = document.createElement('a');
    link.href = url;
    link.download = attachment.fileName;
    link.click();
    URL.revokeObjectURL(url);
  },
};
