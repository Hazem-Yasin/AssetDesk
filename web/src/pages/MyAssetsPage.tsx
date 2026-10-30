import { useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api';
import { Card, ErrorBanner, Spinner } from '../components';
import { errorMessage, formatDate, useAsync } from '../hooks';

export default function MyAssetsPage() {
  const mine = useAsync(() => api.mine(), []);
  const [error, setError] = useState<string | null>(null);

  async function giveBack(id: number) {
    setError(null);
    try {
      await api.checkIn(id);
      mine.reload();
    } catch (e) {
      setError(errorMessage(e));
    }
  }

  return (
    <>
      <h1>My assets</h1>
      <ErrorBanner message={error ?? mine.error} />
      <Card>
        {mine.loading && !mine.data ? <Spinner /> : mine.data?.length ? (
          <table>
            <thead><tr><th>Asset</th><th>Due</th><th /></tr></thead>
            <tbody>
              {mine.data.map((a) => (
                <tr key={a.id}>
                  <td><Link to={`/assets/${a.id}`}>{a.tag} · {a.name}</Link></td>
                  <td className={a.currentAssignment?.isOverdue ? 'danger' : ''}>
                    {a.currentAssignment ? formatDate(a.currentAssignment.dueAt) : '—'}
                    {a.currentAssignment?.isOverdue && ' (overdue)'}
                  </td>
                  <td className="num"><button onClick={() => giveBack(a.id)}>Return</button></td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : (
          <p className="muted">You have nothing checked out.</p>
        )}
      </Card>
    </>
  );
}
