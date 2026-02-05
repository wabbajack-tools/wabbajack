import { useState, useMemo, useCallback } from 'react';
import { ModlistMetadata, getGameDisplayName } from '../api/types';

export interface GalleryFilters {
  search: string;
  game: string;
  showNsfw: boolean;
  featuredOnly: boolean;
}

export interface GameWithCount {
  gameId: string;
  count: number;
}

const defaultFilters: GalleryFilters = {
  search: '',
  game: 'all',
  showNsfw: false,
  featuredOnly: false,
};

export function useGalleryFilters(modlists: ModlistMetadata[]) {
  const [filters, setFilters] = useState<GalleryFilters>(defaultFilters);

  const setSearch = useCallback((search: string) => {
    setFilters(prev => ({ ...prev, search }));
  }, []);

  const setGame = useCallback((game: string) => {
    setFilters(prev => ({ ...prev, game }));
  }, []);

  const setShowNsfw = useCallback((showNsfw: boolean) => {
    setFilters(prev => ({ ...prev, showNsfw }));
  }, []);

  const setFeaturedOnly = useCallback((featuredOnly: boolean) => {
    setFilters(prev => ({ ...prev, featuredOnly }));
  }, []);

  const resetFilters = useCallback(() => {
    setFilters(defaultFilters);
  }, []);

  // Apply filters to get filtered modlists
  const filteredModlists = useMemo(() => {
    return modlists.filter(m => {
      // NSFW filter
      if (!filters.showNsfw && m.nsfw) return false;

      // Featured filter
      if (filters.featuredOnly && !m.official) return false;

      // Game filter
      if (filters.game !== 'all' && m.game.toLowerCase() !== filters.game.toLowerCase()) {
        return false;
      }

      // Text search filter
      if (filters.search) {
        const searchLower = filters.search.toLowerCase();
        const matches =
          m.title.toLowerCase().includes(searchLower) ||
          m.description.toLowerCase().includes(searchLower) ||
          m.author?.toLowerCase().includes(searchLower) ||
          m.tags?.some(t => t.toLowerCase().includes(searchLower));
        if (!matches) return false;
      }

      return true;
    });
  }, [modlists, filters]);

  // Compute available games from all modlists (respecting NSFW filter for counts)
  const availableGames = useMemo((): GameWithCount[] => {
    // Count games from filtered list (without game filter applied)
    const relevantModlists = modlists.filter(m => {
      if (!filters.showNsfw && m.nsfw) return false;
      if (filters.featuredOnly && !m.official) return false;
      return true;
    });

    const gameMap = new Map<string, { canonicalId: string; count: number }>();

    relevantModlists.forEach(m => {
      const normalizedKey = m.game.toLowerCase();
      const existing = gameMap.get(normalizedKey);

      if (existing) {
        existing.count += 1;
      } else {
        gameMap.set(normalizedKey, { canonicalId: m.game, count: 1 });
      }
    });

    return [...gameMap.values()]
      .map(({ canonicalId, count }) => ({ gameId: canonicalId, count }))
      .sort((a, b) =>
        getGameDisplayName(a.gameId).localeCompare(getGameDisplayName(b.gameId))
      );
  }, [modlists, filters.showNsfw, filters.featuredOnly]);

  return {
    filters,
    filteredModlists,
    availableGames,
    setSearch,
    setGame,
    setShowNsfw,
    setFeaturedOnly,
    resetFilters,
  };
}
