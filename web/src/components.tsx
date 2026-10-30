import type { ReactNode } from 'react';
import type { AssetStatus } from './types';

const LABELS: Record<AssetStatus, string> = {
  Available: 'Available',
  CheckedOut: 'Checked out',
  InMaintenance: 'In maintenance',
  Retired: 'Retired',
};

export function StatusBadge({ status }: { status: AssetStatus }) {
  return <span className={`badge status-${status}`}>{LABELS[status]}</span>;
}

export function ErrorBanner({ message }: { message: string | null }) {
  return message ? <div className="error" role="alert">{message}</div> : null;
}

export function Spinner() {
  return <p className="muted" role="status">Loading…</p>;
}

export function Card({ title, children, actions }: { title?: string; children: ReactNode; actions?: ReactNode }) {
  return (
    <section className="card">
      {(title || actions) && (
        <header className="card-header">
          {title && <h2>{title}</h2>}
          {actions}
        </header>
      )}
      {children}
    </section>
  );
}

export function Pager({
  page, pageSize, total, onPage,
}: { page: number; pageSize: number; total: number; onPage: (page: number) => void }) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  return (
    <nav className="pager" aria-label="Pagination">
      <button disabled={page <= 1} onClick={() => onPage(page - 1)}>Previous</button>
      <span>Page {page} of {pages} · {total} assets</span>
      <button disabled={page >= pages} onClick={() => onPage(page + 1)}>Next</button>
    </nav>
  );
}
