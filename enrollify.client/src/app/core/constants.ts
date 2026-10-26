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

// Badge colours for enrollment statuses (same palette as the enrollments list).
export function enrollmentStatusBadgeClass(status: string): string {
  const classes: Record<string, string> = {
    Draft: 'bg-gray-100 text-gray-700',
    Submitted: 'bg-blue-100 text-blue-700',
    Assessed: 'bg-yellow-100 text-yellow-700',
    Approved: 'bg-purple-100 text-purple-700',
    Paid: 'bg-green-100 text-green-700',
    Enrolled: 'bg-emerald-100 text-emerald-800',
    Cancelled: 'bg-red-100 text-red-700',
  };
  return classes[status] || 'bg-gray-100 text-gray-700';
}

export function enrollmentStatusName(status: string | number): string {
  if (typeof status === 'number') return ENROLLMENT_STATUS_NAMES[status] ?? `${status}`;
  const asNumber = Number(status);
  if (!isNaN(asNumber) && `${asNumber}` === `${status}`.trim()) {
    return ENROLLMENT_STATUS_NAMES[asNumber] ?? status;
  }
  return status;
}

export const LEARNER_TYPES: { value: string; label: string }[] = [
  { value: 'New', label: 'New' },
  { value: 'Continuing', label: 'Continuing' },
  { value: 'Transferee', label: 'Transferee' },
  { value: 'Returning', label: 'Returning (Balik-Aral)' },
];

export const DISABILITY_TYPES: string[] = [
  'Visual Impairment', 'Hearing Impairment', 'Learning Disability', 'Intellectual Disability',
  'Autism Spectrum Disorder', 'Emotional-Behavioral Disorder', 'Orthopedic/Physical Handicap',
  'Speech/Language Disorder', 'Cerebral Palsy', 'Special Health Problem/Chronic Disease',
  'Multiple Disorder', 'Cancer',
];

export const EXTENSION_NAME_SUGGESTIONS: string[] = ['Jr.', 'Sr.', 'II', 'III', 'IV'];

// Suggestions for the guardian "Relationship" field (free text is still allowed).
export const GUARDIAN_RELATIONSHIP_SUGGESTIONS: string[] = ['Mother', 'Father', 'Grandparent', 'Aunt/Uncle', 'Sibling', 'Other relative', 'Non-relative'];

export function isShsGrade(grade: string | null | undefined): boolean {
  const g = (grade ?? '').trim().toLowerCase();
  return g === 'grade 11' || g === 'grade 12';
}
