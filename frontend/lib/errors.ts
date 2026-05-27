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
  // Labels
  "projects:label:name:already_exists": "_form",
  "projects:label:not_found": "_form",
  "projects:label:edit:forbidden": "_form",
  // Issues
  "issues:issue:title:required_or_too_long": "title",
  "issues:issue:description:too_long": "description",
  "issues:issue:acceptance_criteria:too_long": "acceptanceCriteria",
  "issues:issue:priority:invalid": "priority",
  "issues:issue:labels:too_many": "labelIds",
  "issues:issue:labels:not_in_project": "labelIds",
  "issues:issue:assignee:not_found": "assigneeId",
  "issues:issue:status:invalid": "status",
  "issues:issue:not_found": "_form",
  "issues:issue:delete:forbidden": "_form",
  // Comments
  "comments:comment:not_found": "_form",
  "comments:comment:update:forbidden": "_form",
  "comments:comment:delete:forbidden": "_form",
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
