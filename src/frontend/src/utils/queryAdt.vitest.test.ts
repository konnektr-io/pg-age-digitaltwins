import { describe, expect, it } from "vitest";
import { generateQueryWithParameters } from "./queryAdt";
import type { FeatureParameterValues } from "models/FeatureParameter";

describe("generateQueryWithParameters", () => {
  it("should return the original query if no parameters are provided", () => {
    const query = "SELECT * FROM digitaltwins";
    const result = generateQueryWithParameters(query);
    expect(result).toBe(query);
  });

  it("should replace parameters in the query", () => {
    const query =
      "SELECT * FROM digitaltwins WHERE name = '@name' AND age > @age";
    const params: FeatureParameterValues = { name: "Alice", age: "30" };
    const result = generateQueryWithParameters(query, params);
    expect(result).toBe(
      "SELECT * FROM digitaltwins WHERE name = 'Alice' AND age > 30"
    );
  });

  it("should handle JEXL expressions", () => {
    const query =
      '="SELECT * FROM digitaltwins WHERE name = \'" + name + "\' AND age > " + age ';
    const params: FeatureParameterValues = { name: "Alice", age: "30" };
    const result = generateQueryWithParameters(query, params);
    expect(result).toBe(
      "SELECT * FROM digitaltwins WHERE name = 'Alice' AND age > 30"
    );
  });

  it("should handle JEXL expressions with Mustache", () => {
    const query =
      "SELECT * FROM digitaltwins WHERE name = '{{name}}' AND age > {{age}}";
    const params: FeatureParameterValues = { name: "Alice", age: "30" };
    const result = generateQueryWithParameters(query, params);
    expect(result).toBe(
      "SELECT * FROM digitaltwins WHERE name = 'Alice' AND age > 30"
    );
  });
});
