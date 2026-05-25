import { describe, it, expect } from "vitest";
import { slugSchema, projectCreateSchema, projectUpdateSchema } from "@/lib/schemas/projects";

describe("slugSchema", () => {
  it("accepts valid 3-char slug", () => {
    expect(slugSchema.safeParse("abc").success).toBe(true);
  });

  it("accepts valid slug with hyphens", () => {
    expect(slugSchema.safeParse("my-project").success).toBe(true);
  });

  it("accepts exactly 50-char slug", () => {
    const slug = "a" + "b".repeat(48) + "c"; // 50 chars
    expect(slugSchema.safeParse(slug).success).toBe(true);
  });

  it("rejects 1-char slug (below min 3)", () => {
    expect(slugSchema.safeParse("a").success).toBe(false);
  });

  it("rejects 2-char slug (below min 3)", () => {
    expect(slugSchema.safeParse("ab").success).toBe(false);
  });

  it("rejects 51-char slug (above max 50)", () => {
    const slug = "a" + "b".repeat(49) + "c"; // 51 chars
    expect(slugSchema.safeParse(slug).success).toBe(false);
  });

  it("rejects slug with uppercase letters", () => {
    expect(slugSchema.safeParse("My-Project").success).toBe(false);
  });

  it("rejects slug with leading hyphen", () => {
    expect(slugSchema.safeParse("-foo").success).toBe(false);
  });

  it("rejects slug with trailing hyphen", () => {
    expect(slugSchema.safeParse("foo-").success).toBe(false);
  });

  it("rejects empty string", () => {
    expect(slugSchema.safeParse("").success).toBe(false);
  });
});

describe("projectCreateSchema", () => {
  it("accepts valid minimal input", () => {
    expect(projectCreateSchema.safeParse({ name: "X", slug: "my-slug" }).success).toBe(true);
  });

  it("accepts name of exactly 1 char", () => {
    expect(projectCreateSchema.safeParse({ name: "X", slug: "my-slug" }).success).toBe(true);
  });

  it("accepts name of exactly 100 chars", () => {
    const name = "a".repeat(100);
    expect(projectCreateSchema.safeParse({ name, slug: "my-slug" }).success).toBe(true);
  });

  it("rejects empty name", () => {
    expect(projectCreateSchema.safeParse({ name: "", slug: "my-slug" }).success).toBe(false);
  });

  it("rejects name of 101 chars", () => {
    const name = "a".repeat(101);
    expect(projectCreateSchema.safeParse({ name, slug: "my-slug" }).success).toBe(false);
  });

  it("accepts description of exactly 2000 chars", () => {
    const description = "a".repeat(2000);
    expect(projectCreateSchema.safeParse({ name: "X", slug: "my-slug", description }).success).toBe(true);
  });

  it("rejects description of 2001 chars", () => {
    const description = "a".repeat(2001);
    expect(projectCreateSchema.safeParse({ name: "X", slug: "my-slug", description }).success).toBe(false);
  });

  it("accepts undefined description", () => {
    expect(projectCreateSchema.safeParse({ name: "X", slug: "my-slug" }).success).toBe(true);
  });
});

describe("projectUpdateSchema", () => {
  it("accepts valid name only", () => {
    expect(projectUpdateSchema.safeParse({ name: "My Project" }).success).toBe(true);
  });

  it("rejects empty name", () => {
    expect(projectUpdateSchema.safeParse({ name: "" }).success).toBe(false);
  });

  it("accepts name of exactly 100 chars", () => {
    expect(projectUpdateSchema.safeParse({ name: "a".repeat(100) }).success).toBe(true);
  });

  it("rejects name of 101 chars", () => {
    expect(projectUpdateSchema.safeParse({ name: "a".repeat(101) }).success).toBe(false);
  });

  it("accepts description of exactly 2000 chars", () => {
    expect(projectUpdateSchema.safeParse({ name: "X", description: "a".repeat(2000) }).success).toBe(true);
  });

  it("rejects description of 2001 chars", () => {
    expect(projectUpdateSchema.safeParse({ name: "X", description: "a".repeat(2001) }).success).toBe(false);
  });
});
