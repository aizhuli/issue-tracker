import { describe, it, expect } from "vitest";
import { labelSchema } from "@/lib/schemas/labels";

describe("labelSchema — name boundaries", () => {
  it("rejects empty name (length 0)", () => {
    expect(labelSchema.safeParse({ name: "", color: "#aabbcc" }).success).toBe(false);
  });

  it("accepts name of length 1", () => {
    expect(labelSchema.safeParse({ name: "X", color: "#aabbcc" }).success).toBe(true);
  });

  it("accepts name of exactly 40 characters", () => {
    expect(
      labelSchema.safeParse({ name: "a".repeat(40), color: "#aabbcc" }).success
    ).toBe(true);
  });

  it("rejects name of 41 characters", () => {
    expect(
      labelSchema.safeParse({ name: "a".repeat(41), color: "#aabbcc" }).success
    ).toBe(false);
  });
});

describe("labelSchema — color regex", () => {
  it("accepts valid 6-digit hex color #aabbcc", () => {
    expect(labelSchema.safeParse({ name: "bug", color: "#aabbcc" }).success).toBe(true);
  });

  it("accepts valid uppercase hex color #AABBCC", () => {
    expect(labelSchema.safeParse({ name: "bug", color: "#AABBCC" }).success).toBe(true);
  });

  it("accepts mixed case hex color #a1B2c3", () => {
    expect(labelSchema.safeParse({ name: "bug", color: "#a1B2c3" }).success).toBe(true);
  });

  it("rejects color missing # prefix", () => {
    expect(labelSchema.safeParse({ name: "bug", color: "aabbcc" }).success).toBe(false);
  });

  it("rejects 3-digit shorthand #abc (not a valid 6-digit hex)", () => {
    expect(labelSchema.safeParse({ name: "bug", color: "#abc" }).success).toBe(false);
  });

  it("rejects 7-digit color #aabbcc1", () => {
    expect(labelSchema.safeParse({ name: "bug", color: "#aabbcc1" }).success).toBe(false);
  });

  it("rejects empty string color", () => {
    expect(labelSchema.safeParse({ name: "bug", color: "" }).success).toBe(false);
  });

  it("rejects color with invalid characters", () => {
    expect(labelSchema.safeParse({ name: "bug", color: "#zzzzzz" }).success).toBe(false);
  });
});
