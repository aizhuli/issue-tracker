import { describe, it, expect } from "vitest";
import { issueCreateSchema, issueUpdateSchema, issueStatus, issuePriority } from "@/lib/schemas/issues";

describe("issueCreateSchema — title boundaries", () => {
  it("rejects empty title (length 0)", () => {
    expect(issueCreateSchema.safeParse({ title: "" }).success).toBe(false);
  });

  it("accepts title of length 1", () => {
    expect(issueCreateSchema.safeParse({ title: "A" }).success).toBe(true);
  });

  it("accepts title of exactly 200 characters", () => {
    expect(issueCreateSchema.safeParse({ title: "a".repeat(200) }).success).toBe(true);
  });

  it("rejects title of 201 characters", () => {
    expect(issueCreateSchema.safeParse({ title: "a".repeat(201) }).success).toBe(false);
  });
});

describe("issueCreateSchema — description boundaries", () => {
  it("accepts missing description (undefined)", () => {
    expect(issueCreateSchema.safeParse({ title: "T" }).success).toBe(true);
  });

  it("accepts empty description string", () => {
    expect(issueCreateSchema.safeParse({ title: "T", description: "" }).success).toBe(true);
  });

  it("accepts description of exactly 10000 characters", () => {
    expect(
      issueCreateSchema.safeParse({ title: "T", description: "a".repeat(10000) }).success
    ).toBe(true);
  });

  it("rejects description of 10001 characters", () => {
    expect(
      issueCreateSchema.safeParse({ title: "T", description: "a".repeat(10001) }).success
    ).toBe(false);
  });
});

describe("issueStatus enum", () => {
  const validStatuses = ["backlog", "todo", "in-progress", "in-review", "done"];

  for (const status of validStatuses) {
    it(`accepts "${status}"`, () => {
      expect(issueStatus.safeParse(status).success).toBe(true);
    });
  }

  it("rejects unknown status value", () => {
    expect(issueStatus.safeParse("unknown").success).toBe(false);
  });

  it("rejects empty string", () => {
    expect(issueStatus.safeParse("").success).toBe(false);
  });
});

describe("issueUpdateSchema", () => {
  const validUpdate = { title: "T", status: "todo" as const };

  it("accepts a full valid payload including status", () => {
    expect(
      issueUpdateSchema.safeParse({
        title: "Fix bug",
        description: "Some details",
        status: "in-progress",
        priority: "high",
        assigneeId: "user-1",
        labelIds: ["l1"],
        acceptanceCriteria: "- [ ] done",
      }).success
    ).toBe(true);
  });

  it("rejects payload without status", () => {
    expect(issueUpdateSchema.safeParse({ title: "T" }).success).toBe(false);
  });

  const validStatuses = ["backlog", "todo", "in-progress", "in-review", "done"] as const;

  for (const status of validStatuses) {
    it(`accepts status "${status}"`, () => {
      expect(issueUpdateSchema.safeParse({ ...validUpdate, status }).success).toBe(true);
    });
  }

  it("rejects invalid status value", () => {
    expect(issueUpdateSchema.safeParse({ ...validUpdate, status: "open" }).success).toBe(false);
  });
});

describe("issuePriority enum", () => {
  const validPriorities = ["low", "medium", "high", "urgent"];

  for (const priority of validPriorities) {
    it(`accepts "${priority}"`, () => {
      expect(issuePriority.safeParse(priority).success).toBe(true);
    });
  }

  it("rejects unknown priority value", () => {
    expect(issuePriority.safeParse("critical").success).toBe(false);
  });

  it("rejects empty string", () => {
    expect(issuePriority.safeParse("").success).toBe(false);
  });
});
