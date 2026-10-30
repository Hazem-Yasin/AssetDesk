import { useState, type FormEvent } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ApiError, api } from '../api';
import { Card, ErrorBanner, Spinner } from '../components';
import { errorMessage, useAsync } from '../hooks';
import { CATEGORIES, type Asset, type AssetCategory } from '../types';

/** Create (no :id) or edit (with :id) an asset. Admin only. */
export default function AssetFormPage() {
  const { id } = useParams();
  const assetId = id ? Number(id) : null;
  const detail = useAsync(() => (assetId ? api.get(assetId) : Promise.resolve(undefined)), [assetId]);

  if (assetId && detail.loading && !detail.data) return <Spinner />;
  if (assetId && detail.error) return <ErrorBanner message={detail.error} />;
  // key forces the form to re-initialise from the freshly loaded asset (e.g. after a conflict reload).
  return <AssetForm key={detail.data?.asset.version ?? 'new'} asset={detail.data?.asset} onReload={detail.reload} />;
}

function AssetForm({ asset, onReload }: { asset?: Asset; onReload: () => void }) {
  const navigate = useNavigate();
  const [tag, setTag] = useState(asset?.tag ?? '');
  const [name, setName] = useState(asset?.name ?? '');
  const [category, setCategory] = useState<AssetCategory>(asset?.category ?? 'Laptop');
  const [serialNumber, setSerialNumber] = useState(asset?.serialNumber ?? '');
  const [purchaseDate, setPurchaseDate] = useState(asset?.purchaseDate.slice(0, 10) ?? new Date().toISOString().slice(0, 10));
  const [purchasePrice, setPurchasePrice] = useState(String(asset?.purchasePrice ?? 0));
  const [notes, setNotes] = useState(asset?.notes ?? '');
  const [error, setError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);
  const [busy, setBusy] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    setConflict(false);
    const common = {
      name,
      category,
      serialNumber: serialNumber || null,
      purchaseDate,
      purchasePrice: Number(purchasePrice),
      notes: notes || null,
    };
    try {
      const saved = asset
        ? await api.update(asset.id, { ...common, version: asset.version })
        : await api.create({ ...common, tag });
      navigate(`/assets/${saved.id}`);
    } catch (err) {
      if (err instanceof ApiError && err.status === 409 && asset) setConflict(true);
      else setError(errorMessage(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <h1>{asset ? `Edit ${asset.tag}` : 'New asset'}</h1>
      {conflict && (
        <div className="error" role="alert">
          Someone else changed this asset while you were editing.{' '}
          <button type="button" className="link" onClick={onReload}>Reload the latest version</button>
        </div>
      )}
      <ErrorBanner message={error} />
      <Card>
        <form onSubmit={submit} className="form-grid">
          <label>
            Tag
            <input
              value={tag}
              onChange={(e) => setTag(e.target.value)}
              required
              pattern="[A-Za-z0-9\-]{3,20}"
              title="3-20 letters, digits or dashes"
              disabled={!!asset}
            />
          </label>
          <label>
            Name
            <input value={name} onChange={(e) => setName(e.target.value)} required maxLength={150} />
          </label>
          <label>
            Category
            <select value={category} onChange={(e) => setCategory(e.target.value as AssetCategory)}>
              {CATEGORIES.map((c) => <option key={c}>{c}</option>)}
            </select>
          </label>
          <label>
            Serial number
            <input value={serialNumber} onChange={(e) => setSerialNumber(e.target.value)} maxLength={100} />
          </label>
          <label>
            Purchase date
            <input type="date" value={purchaseDate} onChange={(e) => setPurchaseDate(e.target.value)} required />
          </label>
          <label>
            Purchase price (USD)
            <input type="number" min={0} max={1000000} step="0.01" value={purchasePrice} onChange={(e) => setPurchasePrice(e.target.value)} required />
          </label>
          <label className="wide">
            Notes
            <textarea value={notes} onChange={(e) => setNotes(e.target.value)} maxLength={1000} rows={3} />
          </label>
          <div className="wide actions">
            <button type="submit" className="primary" disabled={busy}>{asset ? 'Save changes' : 'Create asset'}</button>
            <button type="button" onClick={() => navigate(-1)}>Cancel</button>
          </div>
        </form>
      </Card>
    </>
  );
}
