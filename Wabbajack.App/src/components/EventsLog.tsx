import { useEvents, ServerEvent } from "../hooks/useEvents";

export function EventsLog() {
  const { isConnected, events, connect, disconnect, clearEvents } = useEvents();

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-xl font-semibold mb-2">Server Events (SSE)</h2>
          <p className="text-slate-400">
            Real-time event stream from the .NET backend.
          </p>
        </div>
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2">
            <span
              className={`w-2 h-2 rounded-full ${
                isConnected ? "bg-green-500 animate-pulse" : "bg-red-500"
              }`}
            />
            <span className="text-sm text-slate-400">
              {isConnected ? "Connected" : "Disconnected"}
            </span>
          </div>
          {isConnected ? (
            <button
              onClick={disconnect}
              className="px-3 py-1 bg-red-900/50 hover:bg-red-900 text-red-300 rounded text-sm transition-colors"
            >
              Disconnect
            </button>
          ) : (
            <button
              onClick={connect}
              className="px-3 py-1 bg-green-900/50 hover:bg-green-900 text-green-300 rounded text-sm transition-colors"
            >
              Connect
            </button>
          )}
          <button
            onClick={clearEvents}
            className="px-3 py-1 bg-slate-700 hover:bg-slate-600 rounded text-sm transition-colors"
          >
            Clear
          </button>
        </div>
      </div>

      <div className="bg-slate-800 border border-slate-700 rounded-lg overflow-hidden">
        <div className="p-3 bg-slate-800/80 border-b border-slate-700 flex items-center justify-between">
          <span className="text-sm text-slate-400">Events ({events.length})</span>
          <span className="text-xs text-slate-500">Showing last 100 events</span>
        </div>

        <div className="max-h-96 overflow-y-auto">
          {events.length === 0 ? (
            <div className="p-8 text-center text-slate-500">
              {isConnected
                ? "Waiting for events..."
                : "Connect to start receiving events"}
            </div>
          ) : (
            <div className="divide-y divide-slate-700">
              {events
                .slice()
                .reverse()
                .map((event, index) => (
                  <EventRow key={events.length - 1 - index} event={event} />
                ))}
            </div>
          )}
        </div>
      </div>

      <div className="p-4 bg-slate-800/50 border border-slate-700 rounded-lg">
        <h3 className="font-medium mb-2">How SSE Works</h3>
        <ul className="text-slate-400 text-sm space-y-1 list-disc list-inside">
          <li>Browser opens persistent connection to /api/events</li>
          <li>Server pushes events in real-time as they occur</li>
          <li>No polling needed - instant updates</li>
          <li>Automatic reconnection on disconnect</li>
        </ul>
      </div>
    </div>
  );
}

function EventRow({ event }: { event: ServerEvent }) {
  const typeColors: Record<string, string> = {
    Connected: "text-green-400",
    Heartbeat: "text-slate-500",
    StatusUpdate: "text-blue-400",
    Progress: "text-purple-400",
    Error: "text-red-400",
  };

  return (
    <div className="p-3 hover:bg-slate-700/50 transition-colors">
      <div className="flex items-center gap-3">
        <span className={`text-sm font-medium ${typeColors[event.type] || "text-slate-300"}`}>
          {event.type}
        </span>
        <span className="text-xs text-slate-500">
          {new Date(event.timestamp).toLocaleTimeString()}
        </span>
      </div>
      {event.data !== null && event.data !== undefined && (
        <pre className="mt-1 text-xs text-slate-400 font-mono overflow-x-auto">
          {JSON.stringify(event.data, null, 2)}
        </pre>
      )}
    </div>
  );
}
