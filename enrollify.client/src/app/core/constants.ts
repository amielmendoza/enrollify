// Single source of truth for values that were previously duplicated (and drifting)
// across components. Grade levels are free-text strings on the backend, so every
// surface must agree on the exact spelling.

export const GRADE_LEVELS: string[] = [
  'Kindergarten',
  'Grade 1', 'Grade 2', 'Grade 3', 'Grade 4', 'Grade 5', 'Grade 6',
  'Grade 7', 'Grade 8', 'Grade 9', 'Grade 10', 'Grade 11', 'Grade 12',
];

// Index-aligned with the backend EnrollmentStatus enum (Draft=0 … Cancelled=6).
export const ENROLLMENT_STATUS_NAMES: string[] = [
  'Draft', 'Submitted', 'Assessed', 'Approved', 'Paid', 'Enrolled', 'Cancelled',
];

// The linear progress statuses shown in step trackers (Cancelled is terminal, not a step).
export const ENROLLMENT_STEP_NAMES: string[] = [
  'Draft', 'Submitted', 'Assessed', 'Approved', 'Paid', 'Enrolled',
];

export function enrollmentStatusName(status: string | number): string {
  if (typeof status === 'number') return ENROLLMENT_STATUS_NAMES[status] ?? `${status}`;
  const asNumber = Number(status);
  if (!isNaN(asNumber) && `${asNumber}` === `${status}`.trim()) {
    return ENROLLMENT_STATUS_NAMES[asNumber] ?? status;
  }
  return status;
}
