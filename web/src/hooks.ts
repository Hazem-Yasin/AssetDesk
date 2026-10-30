import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError } from './api';

/** Formats a UTC ISO string (or date-only string) for display. */
export function formatDate(value: string): string {
  return new Date(value).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
}

export const money = (value: number) =>
  new Intl.NumberFormat(undefined, { style: 'currency', currency: 'USD' }).format(value);

export const errorMessage = (e: unknown): string =>
  e instanceof ApiError || e instanceof Error ? e.message : 'Something went wrong.';

/** Returns the value after it has stopped changing for `delay` ms (used for search-as-you-type). */
export function useDebounced<T>(value: T, delay = 300): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const id = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(id);
  }, [value, delay]);
  return debounced;
}

export interface AsyncState<T> {
  data: T | undefined;
  error: string | null;
  loading: boolean;
  reload: () => void;
}

/** Runs `load` whenever `deps` change; ignores responses from stale requests. */
export function useAsync<T>(load: () => Promise<T>, deps: unknown[]): AsyncState<T> {
  const [data, setData] = useState<T>();
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [tick, setTick] = useState(0);
  const latest = useRef(0);

  useEffect(() => {
    const id = ++latest.current;
    setLoading(true);
    load()
      .then((result) => { if (id === latest.current) { setData(result); setError(null); } })
      .catch((e) => { if (id === latest.current) setError(errorMessage(e)); })
      .finally(() => { if (id === latest.current) setLoading(false); });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, tick]);

  const reload = useCallback(() => setTick((t) => t + 1), []);
  return { data, error, loading, reload };
}
