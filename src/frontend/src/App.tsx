import { Panel, PanelGroup, PanelResizeHandle } from "react-resizable-panels";
import { ThemeProvider } from "@/components/theme-provider";
import { GraphHeader } from "@/components/layout/GraphHeader";
import { ModelSidebar } from "@/components/layout/ModelSidebar";
import { MainContent } from "@/components/layout/MainContent";
import { Inspector } from "@/components/inspector/Inspector";
import { StatusBar } from "@/components/layout/StatusBar";
import { ConnectionStatusBanner } from "@/components/ConnectionStatusBanner";
import { useWorkspaceStore } from "@/stores/workspaceStore";
import { CookieConsent } from "@/components/cookie-consent";

function App() {
  const {
    showLeftPanel,
    showRightPanel,
    leftPanelSize,
    rightPanelSize,
    setPanelSize,
  } = useWorkspaceStore();

  // Set GTM consent using gtag API
  const setGtmConsent = (consent: "accepted" | "declined") => {
    if (typeof window !== "undefined") {
      // Declare window.gtag for TypeScript
      type GtagFn = (
        command: string,
        action: string,
        params: Record<string, string>
      ) => void;
      const gtag = (window as typeof window & { gtag?: GtagFn }).gtag;
      if (gtag) {
        if (consent === "accepted") {
          gtag("consent", "update", {
            ad_storage: "granted",
            analytics_storage: "granted",
          });
        } else {
          gtag("consent", "update", {
            ad_storage: "denied",
            analytics_storage: "denied",
          });
        }
      }
    }
  };

  // Callback for accepting cookies
  const handleAccept = () => {
    setGtmConsent("accepted");
  };

  // Callback for declining cookies
  const handleDecline = () => {
    setGtmConsent("declined");
  };

  return (
    <ThemeProvider defaultTheme="system" storageKey="konnektr-graph-theme">
      <div className="h-screen w-full flex flex-col bg-background text-foreground">
        {/* Header */}
        <GraphHeader />

        {/* Connection Status Banner */}
        <ConnectionStatusBanner />

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
                  onResize={(size: number) => setPanelSize("left", size)}
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
                  onResize={(size: number) => setPanelSize("right", size)}
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

        {/* Cookie Consent Popup */}
        <CookieConsent
          variant="minimal"
          onAcceptCallback={handleAccept}
          onDeclineCallback={handleDecline}
        />
      </div>
    </ThemeProvider>
  );
}

export default App;
