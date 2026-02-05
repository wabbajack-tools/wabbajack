import { useState, useEffect } from 'react';
import { getGameDisplayName } from '../../api/types';
import { GalleryFilters as FiltersType, GameWithCount } from '../../hooks/useGalleryFilters';

interface GalleryFiltersProps {
  filters: FiltersType;
  availableGames: GameWithCount[];
  totalCount: number;
  filteredCount: number;
  onSearchChange: (search: string) => void;
  onGameChange: (game: string) => void;
  onShowNsfwChange: (show: boolean) => void;
  onFeaturedOnlyChange: (featured: boolean) => void;
}

export function GalleryFilters({
  filters,
  availableGames,
  totalCount,
  filteredCount,
  onSearchChange,
  onGameChange,
  onShowNsfwChange,
  onFeaturedOnlyChange,
}: GalleryFiltersProps) {
  // Debounced search input
  const [searchInput, setSearchInput] = useState(filters.search);

  useEffect(() => {
    const timer = setTimeout(() => {
      onSearchChange(searchInput);
    }, 300);
    return () => clearTimeout(timer);
  }, [searchInput, onSearchChange]);

  return (
    <div className="space-y-4">
      {/* Filter bar */}
      <div className="flex flex-wrap items-center gap-4 p-4 rounded-xl bg-surface/60 backdrop-blur-sm border border-neon-purple/20">
        {/* Filter icon and label */}
        <div className="flex items-center gap-2 text-neon-purple">
          <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 4a1 1 0 011-1h16a1 1 0 011 1v2.586a1 1 0 01-.293.707l-6.414 6.414a1 1 0 00-.293.707V17l-4 4v-6.586a1 1 0 00-.293-.707L3.293 7.293A1 1 0 013 6.586V4z" />
          </svg>
          <span className="font-medium text-text-primary">Filters</span>
        </div>

        <div className="h-6 w-px bg-neon-purple/20 hidden sm:block" />

        {/* Search input */}
        <div className="relative flex-1 min-w-[200px] max-w-[300px]">
          <svg className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-text-muted" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
          <input
            type="text"
            placeholder="Search modlists..."
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            className="w-full pl-9 pr-8 py-2 bg-surface border border-neon-purple/20 rounded-lg text-text-primary placeholder-text-muted focus:outline-none focus:border-neon-purple focus:ring-1 focus:ring-neon-purple transition-colors"
          />
          {searchInput && (
            <button
              onClick={() => setSearchInput('')}
              className="absolute right-2 top-1/2 -translate-y-1/2 p-1 rounded hover:bg-surface-light transition-colors"
            >
              <svg className="h-4 w-4 text-text-muted hover:text-text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          )}
        </div>

        <div className="h-6 w-px bg-neon-purple/20 hidden sm:block" />

        {/* Checkboxes */}
        <div className="flex items-center gap-4">
          <label className="flex items-center gap-2 cursor-pointer group">
            <input
              type="checkbox"
              checked={filters.showNsfw}
              onChange={(e) => onShowNsfwChange(e.target.checked)}
              className="w-4 h-4 rounded border-neon-purple/30 bg-surface text-neon-purple focus:ring-neon-purple focus:ring-offset-0 focus:ring-offset-transparent"
            />
            <span className="text-sm text-text-secondary group-hover:text-text-primary transition-colors">
              Show NSFW
            </span>
          </label>

          <label className="flex items-center gap-2 cursor-pointer group">
            <input
              type="checkbox"
              checked={filters.featuredOnly}
              onChange={(e) => onFeaturedOnlyChange(e.target.checked)}
              className="w-4 h-4 rounded border-neon-purple/30 bg-surface text-neon-purple focus:ring-neon-purple focus:ring-offset-0 focus:ring-offset-transparent"
            />
            <span className="text-sm text-text-secondary group-hover:text-text-primary transition-colors">
              Featured Only
            </span>
          </label>
        </div>

        <div className="h-6 w-px bg-neon-purple/20 hidden sm:block" />

        {/* Game dropdown */}
        <div className="flex items-center gap-2">
          <span className="text-sm text-text-secondary">Game:</span>
          <select
            value={filters.game}
            onChange={(e) => onGameChange(e.target.value)}
            className="bg-surface border border-neon-purple/20 rounded-lg px-3 py-2 text-text-primary focus:outline-none focus:border-neon-purple focus:ring-1 focus:ring-neon-purple min-w-[180px] transition-colors"
          >
            <option value="all">All Games</option>
            {availableGames.map(({ gameId, count }) => (
              <option key={gameId} value={gameId}>
                {getGameDisplayName(gameId)} ({count})
              </option>
            ))}
          </select>
        </div>
      </div>

      {/* Results count */}
      <div className="text-sm text-text-muted">
        Showing <span className="text-neon-purple">{filteredCount}</span> of {totalCount} modlists
      </div>
    </div>
  );
}
