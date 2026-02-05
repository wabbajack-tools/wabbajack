import { useState } from 'react';
import { ModlistMetadata } from '../../api/types';
import { ModlistPreInstallInfo } from '../../api/install';
import { useModlists } from '../../hooks/useModlists';
import { useGalleryFilters } from '../../hooks/useGalleryFilters';
import { useModlistPrepare } from '../../hooks/useModlistPrepare';
import { GalleryFilters } from './GalleryFilters';
import { GalleryGrid } from './GalleryGrid';
import { ModlistDetailsPage } from './ModlistDetailsPage';
import { PreInstallChecksPage } from './PreInstallChecksPage';

type GalleryView =
  | { mode: 'list' }
  | { mode: 'details'; modlist: ModlistMetadata }
  | { mode: 'preparing'; modlist: ModlistMetadata }
  | { mode: 'pre-install-checks'; modlist: ModlistMetadata; info: ModlistPreInstallInfo };

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
  const {
    status: prepareStatus,
    info: prepareInfo,
    error: prepareError,
    isLoading: isPreparing,
    prepare,
    reset: resetPrepare,
  } = useModlistPrepare();

  // Handle the install button click from details page
  const handleStartInstall = async (modlist: ModlistMetadata) => {
    setView({ mode: 'preparing', modlist });
    await prepare({
      downloadUrl: modlist.links.download,
      machineUrl: modlist.links.machineURL,
    });
  };

  // Effect to transition from preparing to pre-install-checks when ready
  if (view.mode === 'preparing' && prepareInfo) {
    setView({ mode: 'pre-install-checks', modlist: view.modlist, info: prepareInfo });
  }

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

  // Preparing state - show loading indicator
  if (view.mode === 'preparing') {
    const progress = prepareStatus?.progress ?? 0;
    const statusText =
      prepareStatus?.status === 'downloading'
        ? 'Downloading modlist...'
        : prepareStatus?.status === 'extracting'
          ? 'Extracting modlist data...'
          : 'Preparing...';

    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <div className="glass rounded-2xl p-8 max-w-md w-full border border-neon-purple/20 text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-neon-purple/20 to-neon-pink/20 rounded-full flex items-center justify-center mx-auto mb-6">
            <svg
              className="w-8 h-8 text-neon-purple animate-spin"
              fill="none"
              viewBox="0 0 24 24"
            >
              <circle
                className="opacity-25"
                cx="12"
                cy="12"
                r="10"
                stroke="currentColor"
                strokeWidth="4"
              />
              <path
                className="opacity-75"
                fill="currentColor"
                d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
              />
            </svg>
          </div>

          <h2 className="text-xl font-bold font-display text-text-primary mb-2">
            {view.modlist.title}
          </h2>
          <p className="text-text-muted mb-6">{statusText}</p>

          {/* Progress bar */}
          <div className="w-full bg-surface-light rounded-full h-2 mb-4">
            <div
              className="bg-gradient-to-r from-neon-purple to-neon-pink h-2 rounded-full transition-all duration-200"
              style={{ width: `${Math.round(progress * 100)}%` }}
            />
          </div>
          <p className="text-sm text-text-muted">{Math.round(progress * 100)}%</p>

          {prepareError && (
            <div className="mt-6 p-4 bg-error/20 border border-error/50 rounded-lg text-error text-sm">
              {prepareError}
            </div>
          )}

          <button
            onClick={() => {
              resetPrepare();
              setView({ mode: 'details', modlist: view.modlist });
            }}
            className="mt-6 px-4 py-2 text-text-muted hover:text-text-primary transition-colors"
          >
            Cancel
          </button>
        </div>
      </div>
    );
  }

  // Pre-install checks page
  if (view.mode === 'pre-install-checks') {
    return (
      <PreInstallChecksPage
        info={view.info}
        onProceed={(downloadFolder: string, installFolder: string) => {
          // TODO: Start actual installation with the specified folders
          alert(
            `Installation would start here!\n\n` +
              `Download folder: ${downloadFolder}\n` +
              `Install folder: ${installFolder}\n\n` +
              `This feature is coming soon.`
          );
        }}
        onCancel={() => {
          resetPrepare();
          setView({ mode: 'details', modlist: view.modlist });
        }}
      />
    );
  }

  // Details page
  if (view.mode === 'details') {
    return (
      <ModlistDetailsPage
        modlist={view.modlist}
        onBack={() => setView({ mode: 'list' })}
        onStartInstall={() => handleStartInstall(view.modlist)}
        isPreparingInstall={isPreparing}
      />
    );
  }

  // List view
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
