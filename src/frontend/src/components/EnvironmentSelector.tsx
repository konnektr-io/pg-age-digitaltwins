import { useEnvironmentStore } from "@/stores/environmentStore";

export function EnvironmentSelector(): React.ReactElement {
  const environments = useEnvironmentStore((state) => state.environments);
  const currentEnvironmentId = useEnvironmentStore(
    (state) => state.currentEnvironmentId
  );
  const setCurrentEnvironment = useEnvironmentStore(
    (state) => state.setCurrentEnvironment
  );

  return (
    <select
      value={currentEnvironmentId || ""}
      onChange={(e) => setCurrentEnvironment(e.target.value)}
      className="px-3 py-1 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
      aria-label="Select environment"
    >
      {environments.map((env) => (
        <option key={env.id} value={env.id}>
          {env.name}
        </option>
      ))}
    </select>
  );
}
