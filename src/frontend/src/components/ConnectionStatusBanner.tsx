import { AlertCircle, CheckCircle, XCircle } from "lucide-react";
import { useConnectionStore } from "@/stores/connectionStore";
import { Button } from "@/components/ui/button";

export function ConnectionStatusBanner() {
  const {
    isConnected,
    getCurrentConnection,
    dismissBanner,
    isBannerDismissed,
  } = useConnectionStore();
  const connection = getCurrentConnection();

  // Don't show banner if no connection is selected
  if (!connection) {
    return (
      <div className="bg-yellow-500/10 border-b border-yellow-500/20 px-4 py-2">
        <div className="flex items-center gap-2 text-yellow-600 dark:text-yellow-400">
          <AlertCircle className="w-4 h-4" />
          <span className="text-sm">
            No connection selected. Please select or add a connection.
          </span>
        </div>
      </div>
    );
  }

  // Show banner if selected but not marked as connected (shouldn't happen normally)
  if (!isConnected) {
    return (
      <div className="bg-destructive/10 border-b border-destructive/20 px-4 py-2">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2 text-destructive">
            <XCircle className="w-4 h-4" />
            <span className="text-sm">
              Connection "{connection.name}" is not active
            </span>
          </div>
        </div>
      </div>
    );
  }

  // Show success banner for MSAL/Auth0 connections when first connected
  // (can be hidden after initial confirmation)
  // Handle undefined authProvider (for backwards compatibility or direct API connections)
  const authProvider = connection.authProvider || "none";

  if (authProvider !== "none") {
    // Check if user already dismissed this banner
    if (isBannerDismissed(connection.id)) {
      return null;
    }

    return (
      <div className="bg-green-500/10 border-b border-green-500/20 px-4 py-2">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2 text-green-600 dark:text-green-400">
            <CheckCircle className="w-4 h-4" />
            <span className="text-sm">
              Connected to "{connection.name}" using{" "}
              {authProvider.toUpperCase()} authentication
            </span>
          </div>
          <Button
            variant="ghost"
            size="sm"
            className="h-6 px-2 text-xs"
            onClick={() => dismissBanner(connection.id)}
          >
            Dismiss
          </Button>
        </div>
      </div>
    );
  }

  // Don't show banner for successful NoAuth connections
  return null;
}
