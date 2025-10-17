import {
  Settings,
  LogOut,
  Menu,
  PanelRightOpen,
  PanelLeftOpen,
  Database,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { useWorkspaceStore } from "../../stores/workspaceStore";
// import { useConnectionStore } from "../../stores/connectionStore";
import { ModeToggle } from "../mode-toggle";
import { ConnectionSelector } from "@/components/ConnectionSelector";
import { ConnectionStatus } from "@/components/ConnectionStatus";

export function GraphHeader() {
  const {
    mainView,
    setMainView,
    showRightPanel,
    setShowRightPanel,
    showLeftPanel,
    setShowLeftPanel,
  } = useWorkspaceStore();
  // No longer need isConnected or currentConnection here

  // Mock user data - in real app this would come from auth context
  const mockUser = {
    name: "John Doe",
    email: "john.doe@example.com",
    avatar: null,
  };

  const handleLogout = () => {
    // Mock logout - in real app this would call auth service
    console.log("Logout clicked");
  };

  return (
    <header className="border-b bg-background px-6 py-4">
      <div className="flex items-center justify-between h-10">
        <div className="flex items-center gap-4">
          {/* Left Panel Toggle */}
          {!showLeftPanel && (
            <Button
              variant="ghost"
              size="sm"
              className="p-2"
              onClick={() => setShowLeftPanel(true)}
              title="Open Models Panel"
            >
              <PanelLeftOpen className="w-4 h-4" />
            </Button>
          )}

          {/* Logo and Title - Main Brand */}
          <div className="flex items-center gap-2">
            <Database className="w-5 h-5 text-secondary" />
            <span className="font-semibold text-foreground text-lg">
              Konnektr Graph
            </span>
          </div>

          {/* Connection Selector */}
          <ConnectionSelector />
        </div>

        <div className="flex items-center gap-4">
          {/* View Switcher */}
          <div className="flex gap-1 p-1 bg-muted rounded-md">
            <Button
              variant={mainView === "query" ? "default" : "ghost"}
              size="sm"
              className="px-3 py-1.5 text-xs"
              onClick={() => setMainView("query")}
            >
              Query Explorer
            </Button>
            <Button
              variant={mainView === "models" ? "default" : "ghost"}
              size="sm"
              className="px-3 py-1.5 text-xs"
              onClick={() => setMainView("models")}
            >
              Model Graph
            </Button>
          </div>

          {/* Inspector Panel Toggle */}
          {!showRightPanel && (
            <Button
              variant="ghost"
              size="sm"
              className="p-2"
              onClick={() => setShowRightPanel(true)}
              title="Open Inspector Panel"
            >
              <PanelRightOpen className="w-4 h-4" />
            </Button>
          )}

          {/* Connection Status */}
          <ConnectionStatus />

          {/* Settings Menu */}
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="sm">
                <Menu className="h-4 w-4" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem>
                <Settings className="mr-2 h-4 w-4" />
                Settings
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>

          {/* User Menu */}
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="sm" className="gap-2">
                <Avatar className="h-6 w-6">
                  <AvatarImage
                    src={mockUser.avatar || undefined}
                    alt={mockUser.name}
                  />
                  <AvatarFallback className="text-xs">
                    {mockUser.name
                      .split(" ")
                      .map((n) => n[0])
                      .join("")
                      .toUpperCase()}
                  </AvatarFallback>
                </Avatar>
                <span className="hidden md:inline text-sm">
                  {mockUser.email}
                </span>
                {/* <ChevronDown className="h-4 w-4" /> */}
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem onClick={handleLogout}>
                <LogOut className="mr-2 h-4 w-4" />
                Sign out
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>

          <ModeToggle />
        </div>
      </div>
    </header>
  );
}
