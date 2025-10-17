import { useConnectionStore } from "@/stores/connectionStore";

export function ConnectionSelector(): React.ReactElement {
  const connections = useConnectionStore((state) => state.connections);
  const currentConnectionId = useConnectionStore((state) => state.currentConnectionId);
  const setCurrentConnection = useConnectionStore((state) => state.setCurrentConnection);

  return (
    <select
      value={currentConnectionId || ""}
      onChange={(e) => setCurrentConnection(e.target.value)}
      className="px-3 py-1 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
      aria-label="Select connection"
    >
      {connections.map((conn) => (
        <option key={conn.id} value={conn.id}>
          {conn.name}
        </option>
      ))}
    </select>
  );
}
