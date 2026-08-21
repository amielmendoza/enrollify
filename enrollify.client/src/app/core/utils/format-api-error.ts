/**
 * Turns an API error payload into readable text. The middleware serializes
 * FluentValidation failures as { details: [{ PropertyName, ErrorMessage }] }
 * with PascalCase keys — tolerate camelCase too. Falls back to the payload's
 * top-level error message, then to the caller-supplied fallback.
 *
 * `line` lets a caller decorate each detail line (e.g. the apply page prefixes
 * "Child N:" from the PropertyName's Applicants[n] index); default is the bare message.
 */
export function formatApiError(
  err: any,
  fallback: string,
  line?: (propertyName: string, message: string) => string
): string {
  const details = err?.error?.details;
  if (Array.isArray(details) && details.length > 0) {
    return details
      .map((d: any) => {
        const prop: string = d?.PropertyName ?? d?.propertyName ?? '';
        const msg: string = d?.ErrorMessage ?? d?.errorMessage ?? 'Invalid value.';
        return line ? line(prop, msg) : msg;
      })
      .join('\n');
  }
  return err?.error?.error || fallback;
}
