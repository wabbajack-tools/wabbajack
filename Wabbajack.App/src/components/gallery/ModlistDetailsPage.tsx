import { ModlistMetadata, getGameDisplayName, FALLBACK_MODLIST_IMAGE } from '../../api/types';

interface ModlistDetailsPageProps {
  modlist: ModlistMetadata;
  onBack: () => void;
}

function formatSize(bytes: number | undefined): string {
  if (!bytes) return 'Unknown';
  const gb = bytes / (1024 * 1024 * 1024);
  if (gb >= 1) return `${gb.toFixed(2)} GB`;
  const mb = bytes / (1024 * 1024);
  return `${mb.toFixed(0)} MB`;
}

function formatNumber(num: number | undefined): string {
  if (!num) return 'Unknown';
  return num.toLocaleString();
}

function ExternalLink({ href, children }: { href: string; children: React.ReactNode }) {
  const handleClick = async () => {
    // Use Tauri's opener plugin if available, otherwise window.open
    if (window.__TAURI__) {
      try {
        const { openUrl } = await import('@tauri-apps/plugin-opener');
        await openUrl(href);
      } catch {
        window.open(href, '_blank', 'noopener,noreferrer');
      }
    } else {
      window.open(href, '_blank', 'noopener,noreferrer');
    }
  };

  return (
    <button
      onClick={handleClick}
      className="inline-flex items-center gap-2 px-4 py-2 bg-surface-light/50 hover:bg-surface-light border border-neon-purple/20 hover:border-neon-purple/40 rounded-lg text-text-secondary hover:text-text-primary transition-all duration-200"
    >
      {children}
      <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14" />
      </svg>
    </button>
  );
}

export function ModlistDetailsPage({ modlist, onBack }: ModlistDetailsPageProps) {
  const meta = modlist.download_metadata;

  return (
    <div className="space-y-6">
      {/* Back button */}
      <button
        onClick={onBack}
        className="inline-flex items-center gap-2 text-text-muted hover:text-neon-purple transition-colors"
      >
        <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 19l-7-7m0 0l7-7m-7 7h18" />
        </svg>
        Back to Gallery
      </button>

      {/* Hero image */}
      <div className="relative aspect-[21/9] rounded-2xl overflow-hidden bg-surface">
        <img
          src={modlist.links.image || FALLBACK_MODLIST_IMAGE}
          alt={modlist.title}
          className="w-full h-full object-cover"
          onError={(e) => {
            (e.target as HTMLImageElement).src = FALLBACK_MODLIST_IMAGE;
          }}
        />
        <div className="absolute inset-0 bg-gradient-to-t from-void via-transparent to-transparent" />

        {/* Badges overlay */}
        <div className="absolute bottom-4 left-4 flex items-center gap-2">
          {modlist.official && (
            <span className="bg-gradient-to-r from-neon-purple to-neon-pink text-white text-sm px-3 py-1 rounded-full flex items-center gap-1 glow-sm">
              <svg className="w-4 h-4 fill-current" viewBox="0 0 20 20">
                <path d="M9.049 2.927c.3-.921 1.603-.921 1.902 0l1.07 3.292a1 1 0 00.95.69h3.462c.969 0 1.371 1.24.588 1.81l-2.8 2.034a1 1 0 00-.364 1.118l1.07 3.292c.3.921-.755 1.688-1.54 1.118l-2.8-2.034a1 1 0 00-1.175 0l-2.8 2.034c-.784.57-1.838-.197-1.539-1.118l1.07-3.292a1 1 0 00-.364-1.118L2.98 8.72c-.783-.57-.38-1.81.588-1.81h3.461a1 1 0 00.951-.69l1.07-3.292z" />
              </svg>
              Featured
            </span>
          )}
          {modlist.nsfw && (
            <span className="bg-error text-white text-sm px-3 py-1 rounded-full">
              NSFW
            </span>
          )}
        </div>
      </div>

      {/* Content */}
      <div className="grid gap-6 lg:grid-cols-3">
        {/* Main info */}
        <div className="lg:col-span-2 space-y-6">
          {/* Title and author */}
          <div>
            <h1 className="text-3xl font-bold font-display text-text-primary mb-2">{modlist.title}</h1>
            <p className="text-text-muted">
              by <span className="text-text-secondary">{modlist.author}</span>
              {modlist.maintainers && modlist.maintainers.length > 0 && (
                <span className="text-text-muted"> (maintained by {modlist.maintainers.join(', ')})</span>
              )}
            </p>
          </div>

          {/* Meta row */}
          <div className="flex flex-wrap items-center gap-4 text-sm">
            <span className="bg-surface-light/50 text-text-secondary px-3 py-1 rounded-lg border border-neon-purple/10">
              {getGameDisplayName(modlist.game)}
            </span>
            <span className="text-text-muted">Version {modlist.version}</span>
            {modlist.dateUpdated && (
              <span className="text-text-muted">
                Updated {new Date(modlist.dateUpdated).toLocaleDateString()}
              </span>
            )}
          </div>

          {/* Description */}
          <div>
            <h2 className="text-lg font-semibold font-display text-text-primary mb-2">Description</h2>
            <p className="text-text-secondary leading-relaxed">{modlist.description}</p>
          </div>

          {/* Tags */}
          {modlist.tags && modlist.tags.length > 0 && (
            <div>
              <h2 className="text-lg font-semibold font-display text-text-primary mb-2">Tags</h2>
              <div className="flex flex-wrap gap-2">
                {[...new Set(modlist.tags)].map((tag) => (
                  <span
                    key={tag}
                    className="bg-surface-light/50 text-text-secondary px-3 py-1 rounded-lg border border-neon-purple/10"
                  >
                    {tag}
                  </span>
                ))}
              </div>
            </div>
          )}

          {/* External links */}
          <div>
            <h2 className="text-lg font-semibold font-display text-text-primary mb-3">Links</h2>
            <div className="flex flex-wrap gap-3">
              {modlist.links.readme && (
                <ExternalLink href={modlist.links.readme}>
                  <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253" />
                  </svg>
                  Readme
                </ExternalLink>
              )}
              {modlist.links.discordURL && (
                <ExternalLink href={modlist.links.discordURL}>
                  <svg className="h-4 w-4" fill="currentColor" viewBox="0 0 24 24">
                    <path d="M20.317 4.37a19.791 19.791 0 00-4.885-1.515.074.074 0 00-.079.037c-.21.375-.444.864-.608 1.25a18.27 18.27 0 00-5.487 0 12.64 12.64 0 00-.617-1.25.077.077 0 00-.079-.037A19.736 19.736 0 003.677 4.37a.07.07 0 00-.032.027C.533 9.046-.32 13.58.099 18.057a.082.082 0 00.031.057 19.9 19.9 0 005.993 3.03.078.078 0 00.084-.028c.462-.63.874-1.295 1.226-1.994a.076.076 0 00-.041-.106 13.107 13.107 0 01-1.872-.892.077.077 0 01-.008-.128 10.2 10.2 0 00.372-.292.074.074 0 01.077-.01c3.928 1.793 8.18 1.793 12.062 0a.074.074 0 01.078.01c.12.098.246.198.373.292a.077.077 0 01-.006.127 12.299 12.299 0 01-1.873.892.077.077 0 00-.041.107c.36.698.772 1.362 1.225 1.993a.076.076 0 00.084.028 19.839 19.839 0 006.002-3.03.077.077 0 00.032-.054c.5-5.177-.838-9.674-3.549-13.66a.061.061 0 00-.031-.03zM8.02 15.33c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.956-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.956 2.418-2.157 2.418zm7.975 0c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.956-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.946 2.418-2.157 2.418z"/>
                  </svg>
                  Discord
                </ExternalLink>
              )}
              {modlist.links.websiteURL && (
                <ExternalLink href={modlist.links.websiteURL}>
                  <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 12a9 9 0 01-9 9m9-9a9 9 0 00-9-9m9 9H3m9 9a9 9 0 01-9-9m9 9c1.657 0 3-4.03 3-9s-1.343-9-3-9m0 18c-1.657 0-3-4.03-3-9s1.343-9 3-9m-9 9a9 9 0 019-9" />
                  </svg>
                  Website
                </ExternalLink>
              )}
            </div>
          </div>
        </div>

        {/* Sidebar */}
        <div className="space-y-6">
          {/* Install button */}
          <div className="glass rounded-2xl p-6 border border-neon-purple/20">
            <button
              className="w-full bg-gradient-to-r from-neon-purple to-neon-pink hover:from-neon-pink hover:to-neon-purple text-white font-semibold py-3 px-6 rounded-lg transition-all duration-300 flex items-center justify-center gap-2 neon-glow"
              onClick={() => {
                // TODO: Implement install flow - pass download URL to C# backend
                console.log('Install:', modlist.links.download);
                alert('Install functionality coming soon!\n\nDownload URL:\n' + modlist.links.download);
              }}
            >
              <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
              </svg>
              Install Modlist
            </button>
            <p className="text-xs text-text-muted text-center mt-3">
              Downloads and installs to your game folder
            </p>
          </div>

          {/* Size stats */}
          {meta && (
            <div className="glass rounded-2xl p-6 space-y-4 border border-neon-purple/10">
              <h3 className="font-semibold font-display text-text-primary">Installation Details</h3>

              <div className="space-y-3 text-sm">
                <div className="flex justify-between">
                  <span className="text-text-muted">Download Size</span>
                  <span className="text-text-secondary">{formatSize(meta.Size)}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-text-muted">Total Archives</span>
                  <span className="text-text-secondary">{formatSize(meta.SizeOfArchives)} ({formatNumber(meta.NumberOfArchives)} files)</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-text-muted">Installed Size</span>
                  <span className="text-text-secondary">{formatSize(meta.SizeOfInstalledFiles)}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-text-muted">Total Files</span>
                  <span className="text-text-secondary">{formatNumber(meta.NumberOfInstalledFiles)}</span>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

// Declare Tauri global for TypeScript
declare global {
  interface Window {
    __TAURI__?: object;
  }
}
