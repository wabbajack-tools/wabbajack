import { useState } from "react";
import { HelloTest } from "./components/HelloTest";
import { GamesList } from "./components/GamesList";
import { EventsLog } from "./components/EventsLog";

function App() {
  const [activeTab, setActiveTab] = useState<"hello" | "games" | "events">("hello");

  return (
    <div className="min-h-screen bg-slate-900 text-slate-100">
      {/* Header */}
      <header className="bg-slate-800 border-b border-slate-700 p-4">
        <div className="max-w-4xl mx-auto flex items-center gap-4">
          <h1 className="text-2xl font-bold text-purple-400">Wabbajack</h1>
          <span className="text-slate-400 text-sm">Desktop App</span>
        </div>
      </header>

      {/* Navigation Tabs */}
      <nav className="bg-slate-800/50 border-b border-slate-700">
        <div className="max-w-4xl mx-auto flex gap-1 p-2">
          <TabButton
            active={activeTab === "hello"}
            onClick={() => setActiveTab("hello")}
          >
            Hello Test
          </TabButton>
          <TabButton
            active={activeTab === "games"}
            onClick={() => setActiveTab("games")}
          >
            Games
          </TabButton>
          <TabButton
            active={activeTab === "events"}
            onClick={() => setActiveTab("events")}
          >
            Events
          </TabButton>
        </div>
      </nav>

      {/* Content */}
      <main className="max-w-4xl mx-auto p-6">
        {activeTab === "hello" && <HelloTest />}
        {activeTab === "games" && <GamesList />}
        {activeTab === "events" && <EventsLog />}
      </main>

      {/* Footer */}
      <footer className="fixed bottom-0 left-0 right-0 bg-slate-800 border-t border-slate-700 p-2 text-center text-slate-500 text-sm">
        Connected to wabbajack-cli serve
      </footer>
    </div>
  );
}

function TabButton({
  children,
  active,
  onClick,
}: {
  children: React.ReactNode;
  active: boolean;
  onClick: () => void;
}) {
  return (
    <button
      onClick={onClick}
      className={`px-4 py-2 rounded-lg font-medium transition-colors ${
        active
          ? "bg-purple-600 text-white"
          : "text-slate-400 hover:text-slate-200 hover:bg-slate-700"
      }`}
    >
      {children}
    </button>
  );
}

export default App;
