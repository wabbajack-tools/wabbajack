import { ModlistMetadata } from '../../api/types';
import { ModlistCard } from './ModlistCard';

interface GalleryGridProps {
  modlists: ModlistMetadata[];
  loading: boolean;
  onModlistClick: (modlist: ModlistMetadata) => void;
}

function SkeletonCard() {
  return (
    <div className="h-full flex flex-col rounded-2xl bg-surface/60 border border-neon-purple/10 overflow-hidden animate-pulse">
      <div className="aspect-video bg-surface-light" />
      <div className="p-5 space-y-3">
        <div className="h-5 bg-surface-light rounded w-3/4" />
        <div className="h-4 bg-surface-light rounded w-1/2" />
        <div className="h-4 bg-surface-light rounded w-full" />
        <div className="h-4 bg-surface-light rounded w-2/3" />
      </div>
    </div>
  );
}

export function GalleryGrid({ modlists, loading, onModlistClick }: GalleryGridProps) {
  if (loading) {
    return (
      <div className="grid gap-6 grid-cols-1 md:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4">
        {Array.from({ length: 8 }).map((_, i) => (
          <SkeletonCard key={i} />
        ))}
      </div>
    );
  }

  if (modlists.length === 0) {
    return (
      <div className="text-center py-12">
        <div className="text-text-secondary text-lg mb-2">No modlists found</div>
        <p className="text-text-muted text-sm">Try adjusting your filters</p>
      </div>
    );
  }

  return (
    <div className="grid gap-6 grid-cols-1 md:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4 items-start">
      {modlists.map((modlist) => (
        <ModlistCard
          key={modlist.namespacedName || `${modlist.repositoryName}/${modlist.links.machineURL}`}
          modlist={modlist}
          onClick={() => onModlistClick(modlist)}
        />
      ))}
    </div>
  );
}
