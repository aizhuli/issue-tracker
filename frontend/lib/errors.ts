/**
 * Maps backend error codes to form field names for display/validation error handling.
 * Used in form submission responses to highlight which field(s) failed validation.
 */

type ProblemDetails = {
  errorCode?: string;
  detail?: string;
  [key: string]: unknown;
};

/**
 * Mapping from backend error codes to frontend form field names.
 * Form-level errors use "_form" as the field name.
 */
const ERROR_CODE_TO_FIELD: Record<string, string> = {
  // Projects
  "projects:project:name:required_or_too_long": "name",
  "projects:project:slug:invalid_format": "slug",
  "projects:project:slug:already_exists": "slug",
  "projects:project:description:too_long": "description",
  "projects:project:edit:forbidden": "_form",
  "projects:project:delete:forbidden": "_form",
  "projects:project:not_found": "_form",
  // Auth (for future use)
  "auth:session:missing": "_form",
};

/**
 * Maps a ProblemDetails response to form field errors.
 * Returns a record mapping field names to error messages.
 * If the error code is not recognized, defaults to "_form" (form-level error).
 *
 * @param problem - The ProblemDetails object from a failed API response
 * @returns A record of field names to error messages
 *
 * @example
 * const errors = mapProblemDetailsToFields({
 *   errorCode: "projects:project:slug:already_exists",
 *   detail: "Project slug already exists"
 * });
 * // Returns: { slug: "Project slug already exists" }
 */
export function mapProblemDetailsToFields(
  problem: ProblemDetails
): Record<string, string> {
  const fieldName =
    ERROR_CODE_TO_FIELD[problem.errorCode ?? ""] ?? "_form";
  const message = problem.detail ?? "An error occurred";

  return {
    [fieldName]: message,
  };
}
