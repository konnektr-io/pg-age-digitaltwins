import { Panel, PanelGroup, PanelResizeHandle } from "react-resizable-panels";
import { ThemeProvider } from "@/components/theme-provider";
import { GraphHeader } from "@/components/layout/GraphHeader";
import { ModelSidebar } from "@/components/layout/ModelSidebar";
import { MainContent } from "@/components/layout/MainContent";
import { Inspector } from "@/components/inspector/Inspector";
import { StatusBar } from "@/components/layout/StatusBar";
import { useWorkspaceStore } from "@/stores/workspaceStore";

function App() {
  const {
    showLeftPanel,
    showRightPanel,
    leftPanelSize,
    rightPanelSize,
    setPanelSize,
  } = useWorkspaceStore();

  return (
    <ThemeProvider defaultTheme="system" storageKey="konnektr-graph-theme">
      <div className="h-screen w-full flex flex-col bg-background text-foreground">
        {/* Header */}
        <GraphHeader />

        {/* Main Content Area with Resizable Panels */}
        <div className="flex-1 overflow-hidden">
          <PanelGroup
            direction="horizontal"
            className="h-full"
            key={`${showLeftPanel}-${showRightPanel}`} // Force re-render when panels change
          >
            {/* Left Sidebar */}
            {showLeftPanel && (
              <>
                <Panel
                  id="left-panel"
                  defaultSize={leftPanelSize}
                  minSize={15}
                  maxSize={40}
                  onResize={(size) => setPanelSize("left", size)}
                  className="flex"
                >
                  <ModelSidebar />
                </Panel>
                <PanelResizeHandle className="w-1 bg-border hover:bg-border/80 transition-colors data-[resize-handle-active]:bg-primary" />
              </>
            )}

            {/* Center Content */}
            <Panel id="center-panel" minSize={30} className="flex">
              <MainContent />
            </Panel>

            {/* Right Inspector Panel */}
            {showRightPanel && (
              <>
                <PanelResizeHandle className="w-1 bg-border hover:bg-border/80 transition-colors data-[resize-handle-active]:bg-primary" />
                <Panel
                  id="right-panel"
                  defaultSize={rightPanelSize}
                  minSize={15}
                  maxSize={40}
                  onResize={(size) => setPanelSize("right", size)}
                  className="flex flex-col"
                >
                  <Inspector />
                </Panel>
              </>
            )}
          </PanelGroup>
        </div>

        {/* Status Bar */}
        <StatusBar />
      </div>
    </ThemeProvider>
  );
}

export default App;
