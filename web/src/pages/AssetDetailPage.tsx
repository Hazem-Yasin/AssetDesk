import { useRef, useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { api } from '../api';
import { useAuth } from '../auth';
import { Card, ErrorBanner, Spinner, StatusBadge } from '../components';
import { errorMessage, formatDate, money, useAsync } from '../hooks';
import type { Asset } from '../types';

const inDays = (days: number) => new Date(Date.now() + days * 86_400_000).toISOString().slice(0, 10);

export default function AssetDetailPage() {
  const { id } = useParams();
  const assetId = Number(id);
  const { user, isAdmin } = useAuth();
  const navigate = useNavigate();
  const detail = useAsync(() => api.get(assetId), [assetId]);
  const users = useAsync(() => (isAdmin ? api.users() : Promise.resolve([])), [isAdmin]);
  const [error, setError] = useState<string | null>(null);
  const [dueDate, setDueDate] = useState(inDays(14));
  const [assignee, setAssignee] = useState('');
  const [notes, setNotes] = useState('');
  const fileInput = useRef<HTMLInputElement>(null);

  if (detail.loading && !detail.data) return <Spinner />;
  if (!detail.data) return <ErrorBanner message={detail.error ?? 'Asset not found.'} />;

  const { asset, history, attachments } = detail.data;
  const holder = asset.currentAssignment;

  /** Runs an action, then refreshes the page data; shows the server's message on failure. */
  async function run(action: () => Promise<unknown>) {
    setError(null);
    try {
      await action();
      detail.reload();
    } catch (e) {
      setError(errorMessage(e));
    }
  }

  const checkOut = (e: FormEvent) => {
    e.preventDefault();
    const userId = isAdmin ? Number(assignee) : user!.id;
    // Due at end of the chosen day, UTC.
    return run(() => api.checkOut(asset.id, userId, new Date(`${dueDate}T23:59:59Z`).toISOString(), notes || null));
  };

  const canReturn = !!holder && (isAdmin || holder.userId === user?.id);

  return (
    <>
      <div className="page-title">
        <h1>{asset.tag} · {asset.name} <StatusBadge status={asset.status} /></h1>
        {isAdmin && asset.status !== 'Retired' && <Link className="button" to={`/assets/${asset.id}/edit`}>Edit</Link>}
      </div>
      <ErrorBanner message={error ?? detail.error} />

      <div className="two-col">
        <Card title="Details">
          <dl>
            <dt>Category</dt><dd>{asset.category}</dd>
            <dt>Serial number</dt><dd>{asset.serialNumber ?? '—'}</dd>
            <dt>Purchased</dt><dd>{formatDate(asset.purchaseDate)} for {money(asset.purchasePrice)}</dd>
            <dt>Notes</dt><dd>{asset.notes ?? '—'}</dd>
            <dt>Held by</dt>
            <dd>{holder ? `${holder.userName}, due ${formatDate(holder.dueAt)}${holder.isOverdue ? ' (overdue)' : ''}` : '—'}</dd>
          </dl>
        </Card>

        <Card title="Actions">
          {asset.status === 'Available' && (
            <form onSubmit={checkOut} className="stack">
              {isAdmin && (
                <label>
                  Assign to
                  <select value={assignee} onChange={(e) => setAssignee(e.target.value)} required>
                    <option value="">Select a person…</option>
                    {users.data?.map((u) => <option key={u.id} value={u.id}>{u.displayName}</option>)}
                  </select>
                </label>
              )}
              <label>
                Due date
                <input type="date" value={dueDate} min={inDays(1)} max={inDays(89)} onChange={(e) => setDueDate(e.target.value)} required />
              </label>
              <label>
                Notes
                <input value={notes} onChange={(e) => setNotes(e.target.value)} maxLength={500} />
              </label>
              <button type="submit" className="primary">{isAdmin ? 'Check out' : 'Check out to me'}</button>
            </form>
          )}
          {canReturn && <button className="primary" onClick={() => run(() => api.checkIn(asset.id))}>Check in</button>}
          {isAdmin && <AdminActions asset={asset} run={run} onDeleted={() => navigate('/assets')} />}
          {!isAdmin && asset.status !== 'Available' && !canReturn && <p className="muted">This asset is not available.</p>}
        </Card>
      </div>

      <Card
        title="Attachments"
        actions={isAdmin && (
          <>
            <input
              ref={fileInput}
              type="file"
              accept=".pdf,.png,.jpg,.jpeg,.txt"
              aria-label="Upload attachment"
              onChange={(e) => {
                const file = e.target.files?.[0];
                if (file) run(() => api.upload(asset.id, file)).finally(() => { if (fileInput.current) fileInput.current.value = ''; });
              }}
            />
          </>
        )}
      >
        {attachments.length ? (
          <ul className="plain">
            {attachments.map((a) => (
              <li key={a.id}>
                <button className="link" onClick={() => run(() => api.downloadAttachment(asset.id, a))}>{a.fileName}</button>
                <span className="muted">{Math.ceil(a.sizeBytes / 1024)} KB · {formatDate(a.uploadedAt)}</span>
                {isAdmin && <button onClick={() => run(() => api.deleteAttachment(asset.id, a.id))}>Delete</button>}
              </li>
            ))}
          </ul>
        ) : <p className="muted">No attachments.</p>}
      </Card>

      <Card title="Assignment history">
        {history.length ? (
          <table>
            <thead><tr><th>Person</th><th>Checked out</th><th>Due</th><th>Returned</th></tr></thead>
            <tbody>
              {history.map((h) => (
                <tr key={h.id}>
                  <td>{h.userName}</td>
                  <td>{formatDate(h.checkedOutAt)}</td>
                  <td>{formatDate(h.dueAt)}</td>
                  <td>{h.returnedAt ? formatDate(h.returnedAt) : <em>Still out</em>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : <p className="muted">Never checked out.</p>}
      </Card>
    </>
  );
}

function AdminActions({
  asset, run, onDeleted,
}: { asset: Asset; run: (action: () => Promise<unknown>) => Promise<void>; onDeleted: () => void }) {
  return (
    <div className="actions">
      {asset.status === 'Available' && <button onClick={() => run(() => api.toMaintenance(asset.id))}>Send to maintenance</button>}
      {asset.status === 'InMaintenance' && <button onClick={() => run(() => api.fromMaintenance(asset.id))}>Complete maintenance</button>}
      {asset.status !== 'Retired' && asset.status !== 'CheckedOut' && (
        <button onClick={() => window.confirm('Retire this asset? This cannot be undone.') && run(() => api.retire(asset.id))}>Retire</button>
      )}
      <button
        className="danger-button"
        onClick={() => window.confirm('Delete this asset permanently?') && run(async () => { await api.remove(asset.id); onDeleted(); })}
      >
        Delete
      </button>
    </div>
  );
}
