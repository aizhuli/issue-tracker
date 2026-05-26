import { z } from "zod";

export const commentSchema = z.object({
  body: z
    .string()
    .min(1, "Comment body is required")
    .max(10000, "Comment must be at most 10000 characters"),
});

export type CommentValues = z.infer<typeof commentSchema>;
