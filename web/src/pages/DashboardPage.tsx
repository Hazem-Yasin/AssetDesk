import { Link } from 'react-router-dom';
import { api } from '../api';
import { useAuth } from '../auth';
import { Card, ErrorBanner, Spinner } from '../components';
import { formatDate, money, useAsync } from '../hooks';

export default function DashboardPage() {
  const { isAdmin } = useAuth();
  const stats = useAsync(() => api.stats(), []);
  const overdue = useAsync(() => (isAdmin ? api.overdue() : Promise.resolve([])), [isAdmin]);

  if (stats.loading && !stats.data) return <Spinner />;
  const s = stats.data;

  return (
    <>
      <h1>Dashboard</h1>
      <ErrorBanner message={stats.error ?? overdue.error} />
      {s && (
        <>
          <div className="stat-grid">
            <div className="stat"><span>Total assets</span><strong>{s.totalAssets}</strong></div>
            <div className="stat"><span>Overdue</span><strong className={s.overdue ? 'danger' : ''}>{s.overdue}</strong></div>
            <div className="stat"><span>Inventory value</span><strong>{money(s.totalValue)}</strong></div>
          </div>
          <div className="two-col">
            <Card title="By status">
              <ul className="plain">
                {Object.entries(s.byStatus).map(([k, v]) => <li key={k}><span>{k}</span><strong>{v}</strong></li>)}
              </ul>
            </Card>
            <Card title="By category">
              <ul className="plain">
                {Object.entries(s.byCategory).map(([k, v]) => <li key={k}><span>{k}</span><strong>{v}</strong></li>)}
              </ul>
            </Card>
          </div>
        </>
      )}
      {isAdmin && (
        <Card title="Overdue returns">
          {overdue.data?.length ? (
            <table>
              <thead><tr><th>Asset</th><th>Held by</th><th>Due</th></tr></thead>
              <tbody>
                {overdue.data.map((a) => (
                  <tr key={a.id}>
                    <td><Link to={`/assets/${a.assetId}`}>{a.assetTag} · {a.assetName}</Link></td>
                    <td>{a.userName}</td>
                    <td className="danger">{formatDate(a.dueAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : (
            <p className="muted">Nothing overdue.</p>
          )}
        </Card>
      )}
    </>
  );
}
