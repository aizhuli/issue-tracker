import { describe, it, expect } from "vitest";
import { commentSchema } from "@/lib/schemas/comments";

describe("commentSchema — body boundaries", () => {
  it("rejects empty body (length 0)", () => {
    expect(commentSchema.safeParse({ body: "" }).success).toBe(false);
  });

  it("accepts body of length 1", () => {
    expect(commentSchema.safeParse({ body: "A" }).success).toBe(true);
  });

  it("accepts body of exactly 10000 characters", () => {
    expect(commentSchema.safeParse({ body: "a".repeat(10000) }).success).toBe(true);
  });

  it("rejects body of 10001 characters", () => {
    expect(commentSchema.safeParse({ body: "a".repeat(10001) }).success).toBe(false);
  });

  it("accepts typical multiline markdown body", () => {
    const body = "## Summary\n\nThis is a comment with **markdown**.\n\n- item 1\n- item 2";
    expect(commentSchema.safeParse({ body }).success).toBe(true);
  });

  it("rejects missing body field", () => {
    expect(commentSchema.safeParse({}).success).toBe(false);
  });
});
