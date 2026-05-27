import { z } from "zod";

export const issueStatus = z.enum([
  "backlog",
  "todo",
  "in-progress",
  "in-review",
  "done",
]);

export const issuePriority = z.enum(["low", "medium", "high", "urgent"]);

export const issueCreateSchema = z.object({
  title: z
    .string()
    .min(1, "Title is required")
    .max(200, "Title must be at most 200 characters"),
  description: z.string().max(10000).optional(),
  priority: issuePriority.optional(),
  assigneeId: z.string().optional(),
  labelIds: z
    .array(z.string())
    .max(20, "Cannot add more than 20 labels")
    .optional(),
  acceptanceCriteria: z.string().max(10000).optional(),
});

export const issueUpdateSchema = z.object({
  title: z
    .string()
    .min(1, "Title is required")
    .max(200, "Title must be at most 200 characters"),
  description: z.string().max(10000).optional(),
  priority: issuePriority.optional(),
  assigneeId: z.string().optional(),
  labelIds: z
    .array(z.string())
    .max(20, "Cannot add more than 20 labels")
    .optional(),
  acceptanceCriteria: z.string().max(10000).optional(),
  status: issueStatus,
});

export type IssueCreateValues = z.infer<typeof issueCreateSchema>;
export type IssueUpdateValues = z.infer<typeof issueUpdateSchema>;
export type IssueStatus = z.infer<typeof issueStatus>;
export type IssuePriority = z.infer<typeof issuePriority>;
