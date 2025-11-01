# Testing Guide

## Overview

This project uses [Vitest](https://vitest.dev/) for unit testing, along with [React Testing Library](https://testing-library.com/react) for component testing.

## Running Tests

```bash
# Run tests in watch mode (for development)
pnpm test

# Run tests once (for CI)
pnpm test --run

# Run tests with UI
pnpm test:ui

# Run tests with coverage
pnpm test:coverage
```

## Test Structure

Tests are co-located with the source files using the `.test.ts` or `.test.tsx` extension:

```
src/
├── utils/
│   ├── dataStructureDetector.ts
│   └── dataStructureDetector.test.ts    # Unit tests
├── components/
│   ├── SomeComponent.tsx
│   └── SomeComponent.test.tsx           # Component tests
└── test/
    └── setup.ts                          # Global test setup
```

## Writing Tests

### Unit Tests for Utilities

```typescript
import { describe, it, expect } from "vitest";
import { myFunction } from "./myUtility";

describe("myFunction", () => {
  it("should do something", () => {
    const result = myFunction("input");
    expect(result).toBe("expected output");
  });
});
```

### Component Tests

```typescript
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { MyComponent } from "./MyComponent";

describe("MyComponent", () => {
  it("should render correctly", () => {
    render(<MyComponent />);
    expect(screen.getByText("Hello")).toBeInTheDocument();
  });
});
```

## Test Coverage

Current test coverage focuses on:

- ✅ **Utility Functions**: `dataStructureDetector.ts` - 23 tests covering all exported functions
- ⏳ **Components**: Coming soon
- ⏳ **Stores**: Coming soon

## Existing Tests

### `dataStructureDetector.test.ts`

Comprehensive test suite for the data structure analysis utility:

- **getEntityColumns**: Tests for identifying entity columns in query results
- **getEntityProperties**: Tests for extracting properties from entities
- **getEntityType**: Tests for extracting entity type from metadata
- **analyzeDataStructure**: Tests for data complexity analysis and view recommendations

**Coverage**: 23 tests covering:
- Edge cases (null, empty, undefined inputs)
- Simple data structures
- Nested entities
- Complex data with many columns
- Deep nesting detection
- View mode recommendations

## Known Issues & TODOs

The tests document some current behavior that might need improvement:

1. **getEntityType with null $model**: Currently returns `"null"` instead of `"Entity"`
   - See test: "should handle malformed metadata with null $model"
   - TODO: Fix to return "Entity" for invalid models

2. **Deep nesting without entities**: Deep nesting is detected but complexity stays "simple"
   - See test: "should detect deep nesting (4+ levels)"
   - This is current behavior, may need refinement

## Best Practices

1. **Co-locate tests**: Keep test files next to the code they test
2. **Descriptive names**: Use clear `describe` and `it` descriptions
3. **Test behavior, not implementation**: Focus on what the code does, not how
4. **Edge cases**: Always test null, undefined, empty inputs
5. **Type safety**: No `any` types in tests - follow strict TypeScript rules
6. **Document bugs**: If a test reveals a bug, document it with a TODO comment

## Configuration

### vitest.config.ts

```typescript
import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react-swc";
import path from "path";

export default defineConfig({
  plugins: [react()],
  test: {
    globals: true,
    environment: "jsdom",
    setupFiles: "./src/test/setup.ts",
    css: true,
  },
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
});
```

### Test Setup (src/test/setup.ts)

```typescript
import { expect, afterEach } from "vitest";
import { cleanup } from "@testing-library/react";
import * as matchers from "@testing-library/jest-dom/matchers";

expect.extend(matchers);

afterEach(() => {
  cleanup();
});
```

## CI Integration

Tests should be run in CI before merging:

```yaml
- name: Run tests
  run: pnpm test --run
```

## Next Steps

1. Add tests for `queryResultsTransformer.ts`
2. Add component tests for table views
3. Add store tests for Zustand stores
4. Set up coverage thresholds
5. Add integration tests

---

For more information, see:
- [Vitest Documentation](https://vitest.dev/)
- [React Testing Library](https://testing-library.com/react)
- [Testing Library Best Practices](https://kentcdodds.com/blog/common-mistakes-with-react-testing-library)
