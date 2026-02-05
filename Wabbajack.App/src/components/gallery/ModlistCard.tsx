import { ModlistMetadata, getGameDisplayName, FALLBACK_MODLIST_IMAGE } from '../../api/types';

interface ModlistCardProps {
  modlist: ModlistMetadata;
  onClick: () => void;
}

function formatSize(bytes: number | undefined): string {
  if (!bytes) return '';
  const gb = bytes / (1024 * 1024 * 1024);
  if (gb >= 1) return `${gb.toFixed(1)} GB`;
  const mb = bytes / (1024 * 1024);
  return `${mb.toFixed(0)} MB`;
}

export function ModlistCard({ modlist, onClick }: ModlistCardProps) {
  const downloadSize = modlist.download_metadata?.Size;

  return (
    <div
      onClick={onClick}
      className="group h-full cursor-pointer"
    >
      <div className="h-full flex flex-col rounded-2xl bg-surface/60 backdrop-blur-sm border border-neon-purple/10 overflow-hidden transition-all duration-300 hover:border-neon-purple/30 hover:shadow-[0_0_40px_rgba(168,85,247,0.15)] hover:-translate-y-1">
        {/* Image container */}
        <div className="relative aspect-video overflow-hidden bg-surface-light">
          <img
            src={modlist.links.image || FALLBACK_MODLIST_IMAGE}
            alt={modlist.title}
            loading="lazy"
            className="w-full h-full object-cover transition-transform duration-500 group-hover:scale-105"
            onError={(e) => {
              (e.target as HTMLImageElement).src = FALLBACK_MODLIST_IMAGE;
            }}
          />
          {/* Overlay gradient */}
          <div className="absolute inset-0 bg-gradient-to-t from-surface via-transparent to-transparent opacity-60" />

          {/* Featured corner tag */}
          {modlist.official && (
            <div className="absolute top-0 right-0 overflow-hidden w-8 h-8">
              <div className="absolute top-0 right-0 w-12 h-12 -translate-y-1/2 translate-x-1/2 rotate-45 bg-gradient-to-br from-neon-purple to-neon-pink" />
              <svg className="absolute top-1 right-1 h-3 w-3 text-white fill-white" viewBox="0 0 20 20">
                <path d="M9.049 2.927c.3-.921 1.603-.921 1.902 0l1.07 3.292a1 1 0 00.95.69h3.462c.969 0 1.371 1.24.588 1.81l-2.8 2.034a1 1 0 00-.364 1.118l1.07 3.292c.3.921-.755 1.688-1.54 1.118l-2.8-2.034a1 1 0 00-1.175 0l-2.8 2.034c-.784.57-1.838-.197-1.539-1.118l1.07-3.292a1 1 0 00-.364-1.118L2.98 8.72c-.783-.57-.38-1.81.588-1.81h3.461a1 1 0 00.951-.69l1.07-3.292z" />
              </svg>
            </div>
          )}

          {/* Game badge */}
          <div className="absolute bottom-3 left-3">
            <span className="bg-surface/90 backdrop-blur-sm text-text-secondary text-xs px-2 py-1 rounded">
              {getGameDisplayName(modlist.game)}
            </span>
          </div>

          {/* NSFW badge */}
          {modlist.nsfw && (
            <div className="absolute bottom-3 right-3">
              <span className="bg-error/90 text-white text-xs px-2 py-1 rounded">
                NSFW
              </span>
            </div>
          )}
        </div>

        {/* Content */}
        <div className="p-5 flex flex-col flex-1">
          <h3 className="font-display font-bold text-xl text-text-primary mb-1 line-clamp-1">
            {modlist.title}
          </h3>

          <p className="text-sm text-text-muted mb-3">
            by <span className="text-text-secondary">{modlist.author}</span>
          </p>

          <p className="text-sm text-text-secondary mb-4 line-clamp-2 flex-1">
            {modlist.description}
          </p>

          {/* Tags */}
          {modlist.tags && modlist.tags.length > 0 && (
            <div className="flex flex-wrap gap-1.5 mb-4">
              {[...new Set(modlist.tags)].slice(0, 3).map((tag) => (
                <span
                  key={tag}
                  className="bg-surface-light/50 text-text-secondary text-xs px-2 py-0.5 rounded border border-neon-purple/10"
                >
                  {tag}
                </span>
              ))}
              {modlist.tags.length > 3 && (
                <span className="bg-surface-light/50 text-text-muted text-xs px-2 py-0.5 rounded border border-neon-purple/10">
                  +{modlist.tags.length - 3}
                </span>
              )}
            </div>
          )}

          {/* Footer with size */}
          {downloadSize && (
            <div className="pt-3 border-t border-neon-purple/10">
              <span className="text-xs text-text-muted">
                {formatSize(downloadSize)} download
              </span>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
