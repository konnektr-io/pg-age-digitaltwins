import type {
  BasicDigitalTwin,
  BasicRelationship,
  DigitalTwinsModelDataExtended,
} from "@/types";

// Mock Digital Twins Data
export const mockDigitalTwins: BasicDigitalTwin[] = [
  {
    $dtId: "building-001",
    $metadata: {
      $model: "dtmi:example:Building;1",
      name: {
        lastUpdateTime: "2024-01-15T10:30:00Z",
      },
      address: {
        lastUpdateTime: "2024-01-15T10:30:00Z",
      },
    },
    name: "Main Office Building",
    address: "123 Business Ave, Seattle, WA",
    floors: 12,
    yearBuilt: 2018,
    totalArea: 50000,
  },
  {
    $dtId: "floor-001-01",
    $metadata: {
      $model: "dtmi:example:Floor;1",
      name: {
        lastUpdateTime: "2024-01-15T10:31:00Z",
      },
      floorNumber: {
        lastUpdateTime: "2024-01-15T10:31:00Z",
      },
    },
    name: "Ground Floor",
    floorNumber: 1,
    area: 4200,
    occupancyLimit: 150,
  },
  {
    $dtId: "room-001-01-001",
    $metadata: {
      $model: "dtmi:example:Room;1",
      name: {
        lastUpdateTime: "2024-01-15T10:32:00Z",
      },
      temperature: {
        lastUpdateTime: "2024-01-15T11:45:00Z",
      },
    },
    name: "Conference Room A",
    roomNumber: "101A",
    area: 45,
    capacity: 12,
    temperature: 22.5,
    humidity: 45,
    occupancy: 8,
  },
  {
    $dtId: "hvac-001-01",
    $metadata: {
      $model: "dtmi:example:HVAC;1",
      name: {
        lastUpdateTime: "2024-01-15T10:33:00Z",
      },
      temperature: {
        lastUpdateTime: "2024-01-15T11:46:00Z",
      },
    },
    name: "HVAC Unit Floor 1",
    serialNumber: "HVAC-2023-001",
    temperature: 21.8,
    airFlow: 1200,
    energyConsumption: 4.2,
    isActive: true,
  },
  {
    $dtId: "sensor-temp-001",
    $metadata: {
      $model: "dtmi:example:TemperatureSensor;1",
      temperature: {
        lastUpdateTime: "2024-01-15T11:47:00Z",
        sourceTime: "2024-01-15T11:47:00Z",
      },
    },
    name: "Temperature Sensor 001",
    sensorId: "TEMP-001",
    temperature: 22.3,
    batteryLevel: 85,
    lastReading: "2024-01-15T11:47:00Z",
  },
];

// Mock Relationships
export const mockRelationships: BasicRelationship[] = [
  {
    $relationshipId: "rel-building-floor-001",
    $relationshipName: "contains",
    $sourceId: "building-001",
    $targetId: "floor-001-01",
  },
  {
    $relationshipId: "rel-floor-room-001",
    $relationshipName: "contains",
    $sourceId: "floor-001-01",
    $targetId: "room-001-01-001",
  },
  {
    $relationshipId: "rel-floor-hvac-001",
    $relationshipName: "serviced_by",
    $sourceId: "floor-001-01",
    $targetId: "hvac-001-01",
  },
  {
    $relationshipId: "rel-room-sensor-001",
    $relationshipName: "monitored_by",
    $sourceId: "room-001-01-001",
    $targetId: "sensor-temp-001",
  },
  {
    $relationshipId: "rel-hvac-sensor-001",
    $relationshipName: "controls",
    $sourceId: "hvac-001-01",
    $targetId: "sensor-temp-001",
  },
];

// Mock Models
export const mockModels: DigitalTwinsModelDataExtended[] = [
  {
    id: "dtmi:example:Building;1",
    displayName: { en: "Building" },
    description: { en: "A commercial or residential building" },
    uploadTime: new Date("2024-01-01T00:00:00Z"),
    decommissioned: false,
    model: {
      "@context": ["dtmi:dtdl:context;2"],
      "@id": "dtmi:example:Building;1",
      "@type": "Interface",
      displayName: "Building",
      description: "A commercial or residential building",
      contents: [
        {
          "@type": "Property",
          name: "name",
          displayName: "Name",
          schema: "string",
        },
        {
          "@type": "Property",
          name: "address",
          displayName: "Address",
          schema: "string",
        },
        {
          "@type": "Property",
          name: "floors",
          displayName: "Number of Floors",
          schema: "integer",
        },
        {
          "@type": "Relationship",
          name: "contains",
          displayName: "Contains",
          target: "dtmi:example:Floor;1",
          properties: [],
        },
      ],
      extends: [],
    },
    properties: [
      {
        "@type": "Property",
        name: "name",
        displayName: "Name",
        schema: "string",
      },
    ],
    relationships: [
      {
        "@type": "Relationship",
        name: "contains",
        displayName: "Contains",
        target: "dtmi:example:Floor;1",
        properties: [],
      },
    ],
  },
  {
    id: "dtmi:example:Floor;1",
    displayName: { en: "Floor" },
    description: { en: "A floor within a building" },
    uploadTime: new Date("2024-01-01T00:00:00Z"),
    decommissioned: false,
    model: {
      "@context": ["dtmi:dtdl:context;2"],
      "@id": "dtmi:example:Floor;1",
      "@type": "Interface",
      displayName: "Floor",
      description: "A floor within a building",
      contents: [
        {
          "@type": "Property",
          name: "name",
          displayName: "Name",
          schema: "string",
        },
        {
          "@type": "Property",
          name: "floorNumber",
          displayName: "Floor Number",
          schema: "integer",
        },
        {
          "@type": "Relationship",
          name: "contains",
          displayName: "Contains",
          target: "dtmi:example:Room;1",
          properties: [],
        },
      ],
      extends: [],
    },
  },
  {
    id: "dtmi:example:Room;1",
    displayName: { en: "Room" },
    description: { en: "A room within a floor" },
    uploadTime: new Date("2024-01-01T00:00:00Z"),
    decommissioned: false,
    model: {
      "@context": ["dtmi:dtdl:context;2"],
      "@id": "dtmi:example:Room;1",
      "@type": "Interface",
      displayName: "Room",
      description: "A room within a floor",
      contents: [
        {
          "@type": "Property",
          name: "name",
          displayName: "Name",
          schema: "string",
        },
        {
          "@type": "Property",
          name: "temperature",
          displayName: "Temperature",
          schema: "double",
        },
        {
          "@type": "Telemetry",
          name: "occupancy",
          displayName: "Current Occupancy",
          schema: "integer",
        },
      ],
      extends: [],
    },
  },
];

// Mock Query Results (simulating different query patterns)
export const mockQueryResults = {
  // Simple twin query: MATCH (n:Building) RETURN n
  singleTwins: mockDigitalTwins.filter(
    (t) => t.$metadata.$model === "dtmi:example:Building;1"
  ),

  // Relationship query: MATCH (b:Building)-[r:contains]->(f:Floor) RETURN b, r, f
  twinRelationshipResults: [
    {
      b: mockDigitalTwins.find((t) => t.$dtId === "building-001"),
      r: mockRelationships.find(
        (r) =>
          r.$relationshipName === "contains" && r.$sourceId === "building-001"
      ),
      f: mockDigitalTwins.find((t) => t.$dtId === "floor-001-01"),
    },
  ],

  // Multi-level query: MATCH (b:Building)-[:contains*1..2]->(rooms) RETURN b, collect(rooms) as rooms
  nestedResults: [
    {
      b: mockDigitalTwins.find((t) => t.$dtId === "building-001"),
      rooms: mockDigitalTwins.filter(
        (t) =>
          t.$metadata.$model === "dtmi:example:Room;1" ||
          t.$metadata.$model === "dtmi:example:Floor;1"
      ),
    },
  ],

  // Property aggregation: MATCH (r:Room) RETURN avg(r.temperature) as avgTemp, count(r) as roomCount
  aggregationResults: [
    {
      avgTemp: 22.4,
      roomCount: 1,
      maxTemp: 22.5,
      minTemp: 22.3,
    },
  ],
};

// Helper function to get display name from model
export const getModelDisplayName = (modelId: string): string => {
  const model = mockModels.find((m) => m.id === modelId);
  if (model?.displayName) {
    if (typeof model.displayName === "string") return model.displayName;
    const displayNames = model.displayName as Record<string, string>;
    return displayNames.en || Object.values(displayNames)[0] || modelId;
  }
  return modelId.split(";")[0].split(":").pop() || modelId;
};

// Helper function to format twin for display - maintains BasicDigitalTwin structure
export const formatTwinForDisplay = (twin: BasicDigitalTwin) => {
  // Return the twin as-is, maintaining the correct BasicDigitalTwin structure
  // The displayName should be computed in the UI components when needed
  return twin;
};
