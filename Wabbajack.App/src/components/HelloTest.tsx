import { useState } from "react";
import { hello, HelloResponse, ApiResponse } from "../api/client";

export function HelloTest() {
  const [name, setName] = useState("");
  const [response, setResponse] = useState<ApiResponse<HelloResponse> | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    setError(null);

    try {
      const result = await hello(name || undefined);
      setResponse(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to connect to server");
      setResponse(null);
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-semibold mb-2">Hello API Test</h2>
        <p className="text-slate-400">
          Test the round-trip communication between the React frontend and the
          .NET backend.
        </p>
      </div>

      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label htmlFor="name" className="block text-sm font-medium mb-1">
            Your Name (optional)
          </label>
          <input
            id="name"
            type="text"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Enter your name..."
            className="w-full px-4 py-2 bg-slate-800 border border-slate-600 rounded-lg focus:outline-none focus:ring-2 focus:ring-purple-500 focus:border-transparent"
          />
        </div>

        <button
          type="submit"
          disabled={loading}
          className="px-6 py-2 bg-purple-600 hover:bg-purple-700 disabled:bg-slate-600 disabled:cursor-not-allowed rounded-lg font-medium transition-colors"
        >
          {loading ? "Loading..." : "Say Hello"}
        </button>
      </form>

      {error && (
        <div className="p-4 bg-red-900/50 border border-red-700 rounded-lg">
          <p className="text-red-300 font-medium">Error</p>
          <p className="text-red-400">{error}</p>
        </div>
      )}

      {response && (
        <div className="p-4 bg-slate-800 border border-slate-700 rounded-lg space-y-3">
          <div className="flex items-center gap-2">
            <span
              className={`w-2 h-2 rounded-full ${
                response.success ? "bg-green-500" : "bg-red-500"
              }`}
            />
            <span className="font-medium">
              {response.success ? "Success" : "Failed"}
            </span>
          </div>

          {response.data && (
            <div className="space-y-2">
              <div>
                <span className="text-slate-400 text-sm">Message:</span>
                <p className="text-lg font-medium text-purple-300">
                  {response.data.message}
                </p>
              </div>
              <div className="grid grid-cols-2 gap-4 text-sm">
                <div>
                  <span className="text-slate-400">Version:</span>
                  <p>{response.data.version}</p>
                </div>
                <div>
                  <span className="text-slate-400">Timestamp:</span>
                  <p>{new Date(response.data.timestamp).toLocaleString()}</p>
                </div>
              </div>
            </div>
          )}

          {response.error && (
            <p className="text-red-400">{response.error}</p>
          )}
        </div>
      )}

      <div className="p-4 bg-slate-800/50 border border-slate-700 rounded-lg">
        <h3 className="font-medium mb-2">How it works</h3>
        <ol className="text-slate-400 text-sm space-y-1 list-decimal list-inside">
          <li>Frontend sends HTTP GET to /api/hello</li>
          <li>.NET server receives request and generates response</li>
          <li>Response is returned as JSON</li>
          <li>Frontend displays the result</li>
        </ol>
      </div>
    </div>
  );
}
