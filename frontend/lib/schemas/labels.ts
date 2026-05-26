import { z } from "zod";

export const labelSchema = z.object({
  name: z
    .string()
    .min(1, "Name is required")
    .max(40, "Name must be at most 40 characters"),
  color: z
    .string()
    .regex(
      /^#[0-9A-Fa-f]{6}$/,
      "Color must be a valid hex color (e.g. #6B7280)"
    ),
});

export type LabelValues = z.infer<typeof labelSchema>;
