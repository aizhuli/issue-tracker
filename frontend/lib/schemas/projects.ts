import { z } from "zod";

/**
 * Slug validation: lowercase letters, digits, and hyphens only
 * 3–50 chars; no leading/trailing hyphen
 * Matches backend regex: ^[a-z0-9][a-z0-9-]{1,48}[a-z0-9]$
 * Also accepts 3-char slugs via: ^[a-z0-9]([a-z0-9-]{0,48}[a-z0-9])?$
 */
export const slugSchema = z
  .string()
  .min(3, "Slug must be at least 3 characters")
  .max(50, "Slug must be at most 50 characters")
  .regex(
    /^[a-z0-9]([a-z0-9-]{0,48}[a-z0-9])?$/,
    "Slug must contain only lowercase letters, digits, and hyphens, and cannot start or end with a hyphen"
  );

/**
 * Project creation schema
 * Includes: name, slug (required), description (optional)
 */
export const projectCreateSchema = z.object({
  name: z
    .string()
    .min(1, "Name is required")
    .max(100, "Name must be at most 100 characters"),
  slug: slugSchema,
  description: z
    .string()
    .max(2000, "Description must be at most 2000 characters")
    .optional(),
});

/**
 * Project update schema
 * Includes: name, description (optional)
 * Slug is not included — it is the identifier and cannot be changed
 */
export const projectUpdateSchema = z.object({
  name: z
    .string()
    .min(1, "Name is required")
    .max(100, "Name must be at most 100 characters"),
  description: z
    .string()
    .max(2000, "Description must be at most 2000 characters")
    .optional(),
});

export type ProjectCreateValues = z.infer<typeof projectCreateSchema>;
export type ProjectUpdateValues = z.infer<typeof projectUpdateSchema>;
