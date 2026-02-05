import { useState, useEffect } from "react";
import { listGames, GameInfo, ApiResponse } from "../api/client";

export function GamesList() {
  const [games, setGames] = useState<GameInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  async function fetchGames() {
    setLoading(true);
    setError(null);

    try {
      const response: ApiResponse<GameInfo[]> = await listGames();
      if (response.success && response.data) {
        setGames(response.data);
      } else {
        setError(response.error || "Failed to fetch games");
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to connect to server");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    fetchGames();
  }, []);

  const installedGames = games.filter((g) => g.installed);
  const notInstalledGames = games.filter((g) => !g.installed);

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-xl font-semibold mb-2">Installed Games</h2>
          <p className="text-slate-400">
            Games detected on your system that Wabbajack supports.
          </p>
        </div>
        <button
          onClick={fetchGames}
          disabled={loading}
          className="px-4 py-2 bg-slate-700 hover:bg-slate-600 disabled:opacity-50 rounded-lg text-sm transition-colors"
        >
          {loading ? "Refreshing..." : "Refresh"}
        </button>
      </div>

      {error && (
        <div className="p-4 bg-red-900/50 border border-red-700 rounded-lg">
          <p className="text-red-300 font-medium">Error</p>
          <p className="text-red-400">{error}</p>
        </div>
      )}

      {loading && games.length === 0 ? (
        <div className="text-center py-12">
          <div className="animate-spin w-8 h-8 border-2 border-purple-500 border-t-transparent rounded-full mx-auto mb-4" />
          <p className="text-slate-400">Loading games...</p>
        </div>
      ) : (
        <>
          {/* Installed Games */}
          <div className="space-y-2">
            <h3 className="text-sm font-medium text-slate-400 uppercase tracking-wide">
              Installed ({installedGames.length})
            </h3>
            {installedGames.length === 0 ? (
              <p className="text-slate-500 py-4">No supported games found.</p>
            ) : (
              <div className="grid gap-2">
                {installedGames.map((game) => (
                  <GameCard key={game.slug} game={game} />
                ))}
              </div>
            )}
          </div>

          {/* Not Installed Games */}
          <details className="group">
            <summary className="text-sm font-medium text-slate-400 uppercase tracking-wide cursor-pointer hover:text-slate-300">
              Not Installed ({notInstalledGames.length})
              <span className="ml-2 text-xs">Click to expand</span>
            </summary>
            <div className="mt-2 grid gap-2">
              {notInstalledGames.map((game) => (
                <GameCard key={game.slug} game={game} />
              ))}
            </div>
          </details>
        </>
      )}
    </div>
  );
}

function GameCard({ game }: { game: GameInfo }) {
  return (
    <div
      className={`p-4 rounded-lg border transition-colors ${
        game.installed
          ? "bg-slate-800 border-slate-700 hover:border-purple-600"
          : "bg-slate-800/50 border-slate-700/50"
      }`}
    >
      <div className="flex items-start justify-between">
        <div>
          <h4 className={`font-medium ${game.installed ? "" : "text-slate-400"}`}>
            {game.name}
          </h4>
          {game.installed && (
            <p className="text-sm text-slate-400 mt-1">
              Version: {game.version}
            </p>
          )}
        </div>
        <span
          className={`text-xs px-2 py-1 rounded-full ${
            game.installed
              ? "bg-green-900/50 text-green-300"
              : "bg-slate-700 text-slate-400"
          }`}
        >
          {game.installed ? "Installed" : "Not Found"}
        </span>
      </div>
      {game.installed && game.path !== "-" && (
        <p className="text-xs text-slate-500 mt-2 font-mono truncate">
          {game.path}
        </p>
      )}
    </div>
  );
}
