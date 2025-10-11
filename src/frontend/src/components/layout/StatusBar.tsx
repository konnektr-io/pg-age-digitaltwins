import { useConnectionStore } from "@/stores/connectionStore";

export function StatusBar() {
  const { isConnected } = useConnectionStore();

  return (
    <div className="h-8 border-t border-border bg-card flex items-center justify-between px-4 text-xs text-muted-foreground">
      <div className="flex items-center gap-4">
        <span>Ready</span>
        <div className="flex items-center gap-1">
          <div
            className={`w-2 h-2 rounded-full ${
              isConnected ? "bg-green-500" : "bg-red-500"
            }`}
          />
          <span>{isConnected ? "Connected" : "Disconnected"}</span>
        </div>
      </div>
      <div className="flex items-center gap-4">
        <span>Cypher</span>
      </div>
    </div>
  );
}
