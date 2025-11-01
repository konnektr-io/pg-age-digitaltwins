import { useAuth0 } from "@auth0/auth0-react";
import { useConnectionStore } from "../stores/connectionStore";

export function ConnectionStatus(): React.ReactElement {
  const { isAuthenticated, isLoading, loginWithRedirect, logout, user } =
    useAuth0();
  const isConnected = useConnectionStore((state) => state.isConnected);
  const currentConnection = useConnectionStore((state) =>
    state.getCurrentConnection()
  );

  if (isLoading) {
    return <div className="text-sm text-gray-500">Loading...</div>;
  }

  return (
    <div className="flex items-center gap-4">
      {/* Connection Indicator */}
      {currentConnection && (
        <div className="text-sm">
          <span className="text-gray-500">Connection:</span>{" "}
          <span className="font-medium">{currentConnection.name}</span>
        </div>
      )}

      {/* Connection Status */}
      <div className="flex items-center gap-2">
        <div
          className={`h-2 w-2 rounded-full ${
            isAuthenticated && isConnected
              ? "bg-green-500"
              : isAuthenticated
              ? "bg-yellow-500"
              : "bg-red-500"
          }`}
          title={
            isAuthenticated && isConnected
              ? "Connected"
              : isAuthenticated
              ? "Authenticated but not connected"
              : "Disconnected"
          }
        />
        <span className="text-sm text-gray-600">
          {isAuthenticated && isConnected
            ? "Connected"
            : isAuthenticated
            ? "Authenticated"
            : "Not authenticated"}
        </span>
      </div>

      {/* Auth Actions */}
      {isAuthenticated ? (
        <div className="flex items-center gap-2">
          {user && <span className="text-sm text-gray-600">{user.name}</span>}
          <button
            onClick={() =>
              logout({ logoutParams: { returnTo: window.location.origin } })
            }
            className="text-sm text-blue-600 hover:text-blue-800"
          >
            Logout
          </button>
        </div>
      ) : (
        <button
          onClick={() => loginWithRedirect()}
          className="text-sm text-blue-600 hover:text-blue-800"
        >
          Login
        </button>
      )}
    </div>
  );
}
