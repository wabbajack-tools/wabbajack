import { useState } from "react";
import { GalleryPage } from "./components/gallery";

type MainView = "gallery" | "compile" | "install-file";

function App() {
  const [mainView, setMainView] = useState<MainView>("gallery");

  return (
    <div className="min-h-screen grid-bg">
      {/* Header */}
      <header className="glass border-b border-neon-purple/10 p-4">
        <div className="max-w-7xl mx-auto flex items-center gap-4">
          <h1 className="text-2xl font-bold font-display gradient-text">Wabbajack</h1>
          <span className="text-text-muted text-sm">Desktop App</span>
        </div>
      </header>

      {/* Navigation Tabs */}
      <nav className="bg-surface/50 backdrop-blur-sm border-b border-neon-purple/10">
        <div className="max-w-7xl mx-auto flex gap-1 p-2">
          <TabButton
            active={mainView === "gallery"}
            onClick={() => setMainView("gallery")}
          >
            Browse Gallery
          </TabButton>
          <TabButton
            active={mainView === "compile"}
            onClick={() => setMainView("compile")}
          >
            Compile
          </TabButton>
          <TabButton
            active={mainView === "install-file"}
            onClick={() => setMainView("install-file")}
          >
            Install File
          </TabButton>
        </div>
      </nav>

      {/* Content */}
      <main className="max-w-7xl mx-auto p-6 pb-16">
        {mainView === "gallery" && <GalleryPage />}
        {mainView === "compile" && <PlaceholderPage title="Compile" description="Compile a new modlist from your setup" />}
        {mainView === "install-file" && <PlaceholderPage title="Install File" description="Install a .wabbajack file from disk" />}
      </main>

      {/* Footer */}
      <footer className="fixed bottom-0 left-0 right-0 glass border-t border-neon-purple/10 p-2 text-center text-text-muted text-sm">
        Wabbajack Desktop
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
      className={`px-4 py-2 rounded-lg font-medium transition-all duration-300 ${
        active
          ? "bg-gradient-to-r from-neon-purple to-neon-pink text-white glow-sm"
          : "text-text-secondary hover:text-text-primary hover:bg-surface-light"
      }`}
    >
      {children}
    </button>
  );
}

function PlaceholderPage({ title, description }: { title: string; description: string }) {
  return (
    <div className="flex flex-col items-center justify-center py-20">
      <div className="glass rounded-2xl p-8 text-center max-w-md border border-neon-purple/20">
        <div className="w-16 h-16 bg-neon-purple/20 rounded-full flex items-center justify-center mx-auto mb-4">
          <svg className="w-8 h-8 text-neon-purple" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
          </svg>
        </div>
        <h2 className="text-xl font-semibold font-display text-text-primary mb-2">{title}</h2>
        <p className="text-text-secondary">{description}</p>
        <p className="text-text-muted text-sm mt-4">Coming soon</p>
      </div>
    </div>
  );
}

export default App;
