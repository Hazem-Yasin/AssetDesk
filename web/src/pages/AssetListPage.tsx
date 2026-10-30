import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api';
import { useAuth } from '../auth';
import { Card, ErrorBanner, Pager, Spinner, StatusBadge } from '../components';
import { money, useAsync, useDebounced } from '../hooks';
import { CATEGORIES, STATUSES } from '../types';

const PAGE_SIZE = 10;

export default function AssetListPage() {
  const { isAdmin } = useAuth();
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [category, setCategory] = useState('');
  const [page, setPage] = useState(1);
  const debouncedSearch = useDebounced(search);

  // Any filter change goes back to the first page.
  useEffect(() => setPage(1), [debouncedSearch, status, category]);

  const result = useAsync(
    () => api.search({ search: debouncedSearch, status, category, page, pageSize: PAGE_SIZE }),
    [debouncedSearch, status, category, page],
  );

  return (
    <>
      <div className="page-title">
        <h1>Assets</h1>
        {isAdmin && <Link className="button primary" to="/assets/new">New asset</Link>}
      </div>
      <Card>
        <div className="filters">
          <input
            type="search"
            placeholder="Search tag, name or serial…"
            aria-label="Search assets"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          <select aria-label="Status" value={status} onChange={(e) => setStatus(e.target.value)}>
            <option value="">All statuses</option>
            {STATUSES.map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
          <select aria-label="Category" value={category} onChange={(e) => setCategory(e.target.value)}>
            <option value="">All categories</option>
            {CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
          </select>
        </div>
        <ErrorBanner message={result.error} />
        {result.loading && !result.data ? <Spinner /> : (
          <table>
            <thead><tr><th>Tag</th><th>Name</th><th>Category</th><th>Status</th><th>Held by</th><th className="num">Price</th></tr></thead>
            <tbody>
              {result.data?.items.map((a) => (
                <tr key={a.id}>
                  <td><Link to={`/assets/${a.id}`}>{a.tag}</Link></td>
                  <td>{a.name}</td>
                  <td>{a.category}</td>
                  <td><StatusBadge status={a.status} /></td>
                  <td>{a.currentAssignment?.userName ?? '—'}</td>
                  <td className="num">{money(a.purchasePrice)}</td>
                </tr>
              ))}
              {result.data?.items.length === 0 && <tr><td colSpan={6} className="muted">No assets match your filters.</td></tr>}
            </tbody>
          </table>
        )}
        {result.data && <Pager page={page} pageSize={PAGE_SIZE} total={result.data.total} onPage={setPage} />}
      </Card>
    </>
  );
}
