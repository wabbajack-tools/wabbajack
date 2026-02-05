import { useState, useEffect, useCallback } from 'react';
import { ModlistMetadata } from '../api/types';
import { fetchAllModlists } from '../api/modlists';

export function useModlists() {
  const [modlists, setModlists] = useState<ModlistMetadata[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await fetchAllModlists();
      // Filter out force_down modlists
      const activeModlists = data.filter(m => !m.force_down);
      setModlists(activeModlists);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to fetch modlists');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  return {
    modlists,
    loading,
    error,
    refetch: fetchData,
  };
}
