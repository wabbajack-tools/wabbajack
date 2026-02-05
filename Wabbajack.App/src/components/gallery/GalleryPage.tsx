import { useState } from 'react';
import { ModlistMetadata } from '../../api/types';
import { useModlists } from '../../hooks/useModlists';
import { useGalleryFilters } from '../../hooks/useGalleryFilters';
import { GalleryFilters } from './GalleryFilters';
import { GalleryGrid } from './GalleryGrid';
import { ModlistDetailsPage } from './ModlistDetailsPage';

type GalleryView =
  | { mode: 'list' }
  | { mode: 'details'; modlist: ModlistMetadata };

export function GalleryPage() {
  const { modlists, loading, error, refetch } = useModlists();
  const {
    filters,
    filteredModlists,
    availableGames,
    setSearch,
    setGame,
    setShowNsfw,
    setFeaturedOnly,
  } = useGalleryFilters(modlists);

  const [view, setView] = useState<GalleryView>({ mode: 'list' });

  if (error) {
    return (
      <div className="text-center py-12">
        <div className="glass rounded-2xl p-8 max-w-md mx-auto border border-error/20">
          <div className="w-16 h-16 bg-error/20 rounded-full flex items-center justify-center mx-auto mb-4">
            <svg className="w-8 h-8 text-error" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
            </svg>
          </div>
          <div className="text-error text-lg mb-2">Failed to load modlists</div>
          <p className="text-text-muted text-sm mb-4">{error}</p>
          <button
            onClick={refetch}
            className="px-4 py-2 bg-gradient-to-r from-neon-purple to-neon-pink hover:from-neon-pink hover:to-neon-purple text-white rounded-lg transition-all duration-300 neon-glow"
          >
            Retry
          </button>
        </div>
      </div>
    );
  }

  if (view.mode === 'details') {
    return (
      <ModlistDetailsPage
        modlist={view.modlist}
        onBack={() => setView({ mode: 'list' })}
      />
    );
  }

  return (
    <div className="space-y-6">
      <GalleryFilters
        filters={filters}
        availableGames={availableGames}
        totalCount={modlists.length}
        filteredCount={filteredModlists.length}
        onSearchChange={setSearch}
        onGameChange={setGame}
        onShowNsfwChange={setShowNsfw}
        onFeaturedOnlyChange={setFeaturedOnly}
      />
      <GalleryGrid
        modlists={filteredModlists}
        loading={loading}
        onModlistClick={(modlist) => setView({ mode: 'details', modlist })}
      />
    </div>
  );
}
