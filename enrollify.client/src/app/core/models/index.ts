export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  email: string;
  fullName: string;
  role: string;
  tenantId: string;
  // Rotated on every refresh; exchanged at /auth/refresh for a new JWT.
  refreshToken: string;
  // True while the account is on a temporary password — the app quarantines
  // the session to /change-password until it is changed.
  mustChangePassword: boolean;
}

// PSGC-resolved address. Codes are 10-digit strings (never numbers); names travel with them.
export interface PsgcAddress {
  regionCode: string | null;
  regionName: string | null;
  provinceCode: string | null;     // null for NCR / independent cities
  provinceName: string | null;
  cityMunicipalityCode: string | null;
  cityMunicipalityName: string | null;
  barangayCode: string | null;
  barangayName: string | null;
  zipCode: string | null;
}

// DepEd Basic Education Enrollment Form learner fields (LRN travels separately).
export interface LearnerProfile {
  extensionName: string | null;
  psaBirthCertificateNumber: string | null;
  placeOfBirth: string | null;
  motherTongue: string | null;
  religion: string | null;
  isIndigenousPeople: boolean;
  ipCommunity: string | null;
  is4PsBeneficiary: boolean;
  fourPsHouseholdId: string | null;
  hasDisability: boolean;
  disabilityTypes: string[] | null;
  fatherLastName: string | null;
  fatherFirstName: string | null;
  fatherMiddleName: string | null;
  fatherContactNumber: string | null;
  motherMaidenLastName: string | null;
  motherMaidenFirstName: string | null;
  motherMaidenMiddleName: string | null;
  motherContactNumber: string | null;
  homeAddress: PsgcAddress | null;
}

// Per-enrollment DepEd fields.
export interface EnrollmentLearnerInfo {
  learnerType: string | null;
  lastGradeLevelCompleted: string | null;
  lastSchoolYearCompleted: string | null;
  lastSchoolAttended: string | null;
  lastSchoolAddress: string | null;
  lastSchoolId: string | null;
  track: string | null;
  strand: string | null;
}

export interface ShsStrand {
  id: string;
  track: string;
  strand: string;
  isActive: boolean;
  displayOrder: number;
}

export interface Student {
  id: string;
  // Enrollify-issued "YYYY-NNNNN". Never confuse with the DepEd LRN.
  studentNumber: string;
  // DepEd LRN (12 digits); null until known.
  lrn: string | null;
  // Pre-migration LRN awaiting registrar confirmation ("Check LRN").
  lrnNeedsReview: boolean;
  firstName: string;
  middleName: string;
  lastName: string;
  birthDate: string;
  gender: string | null;
  // House no. / street / sitio line; the structured part is profile.homeAddress.
  address: string;
  contactNumber: string | null;
  email: string | null;
  guardianName: string | null;
  guardianContact: string | null;
  fullName: string;
  // Login linkage: the student's own account, and/or the managing parent's.
  userId: string | null;
  parentUserId: string | null;
  isActive: boolean;
  profile: LearnerProfile | null;
}

export interface CreateStudentRequest {
  lrn: string | null;
  firstName: string;
  middleName: string;
  lastName: string;
  birthDate: string;
  gender: string | null;
  address: string;
  contactNumber: string | null;
  email: string | null;
  guardianName: string | null;
  guardianContact: string | null;
  profile: LearnerProfile | null;
}

// What a parent or student may change on their own profile (null/omitted = unchanged).
export interface ProfileSelfUpdate {
  contactNumber?: string;
  email?: string;
  address?: string;
  guardianName?: string;
  guardianContact?: string;
  homeAddress?: PsgcAddress;
  fatherContactNumber?: string;
  motherContactNumber?: string;
}

export interface EnrollmentRequirement {
  id: string;
  documentName: string;
  isSubmitted: boolean;
  fileName: string | null;
  notes: string | null;
  isVerified: boolean;
  verifiedBy: string | null;
  verifiedAt: string | null;
  reviewNotes: string | null;
}

export interface Enrollment {
  id: string;
  studentId: string;
  studentName: string;
  schoolYear: string;
  gradeLevel: string;
  sectionId: string | null;
  sectionName: string | null;
  status: string;
  remarks: string | null;
  createdAt: string;
  paymentPlan: string | null;
  requirements: EnrollmentRequirement[] | null;
  // Only populated on GET /enrollments list rows (not detail/me/child payloads).
  pendingPaymentsCount?: number;
  // Populated on GET /enrollments/{id} (detail + print pages).
  studentNumber?: string | null;
  lrn?: string | null;
  learnerInfo?: EnrollmentLearnerInfo | null;
}

export interface CreateEnrollmentRequest {
  studentId: string;
  schoolYear: string;
  gradeLevel: string;
  learnerType?: string | null;
  track?: string | null;
  strand?: string | null;
}

// One status transition of an enrollment (GET /enrollments/{id}/history, ascending).
export interface EnrollmentHistoryItem {
  fromStatus: string;
  toStatus: string;
  remarks: string | null;
  transitionDate: string;
}

// Student account ledger (statement of account). Chronological; running balance
// is server-computed. Voided adjustments still appear but are excluded from totals.
export type LedgerEntryType = 'Charge' | 'Discount' | 'Interest' | 'Adjustment' | 'Payment';

export interface LedgerEntry {
  date: string;
  type: LedgerEntryType;
  description: string;
  reference: string | null;
  debit: number | null;
  credit: number | null;
  balance: number;
  // Set on adjustment rows so the UI can offer Void; null for other entry types.
  adjustmentId: string | null;
  // Voided adjustment rows arrive with their amounts (rendered struck-through) but the
  // server already excludes them from running balance/totals. Omitted from the printed SOA.
  voided: boolean;
}

export interface EnrollmentLedger {
  entries: LedgerEntry[];
  totalDebits: number;
  totalCredits: number;
  balance: number;
}

export interface Section {
  id: string;
  name: string;
  gradeLevel: string;
  schoolYear: string;
  capacity: number;
  currentCount: number;
  adviser: string | null;
  isActive: boolean;
}

export interface Fee {
  id: string;
  name: string;
  description: string | null;
  amount: number;
  schoolYear: string;
  gradeLevel: string;
  isActive: boolean;
}

export interface Payment {
  id: string;
  enrollmentId: string;
  amount: number;
  paymentMethod: string;
  referenceNumber: string | null;
  remarks: string | null;
  paymentDate: string;
  status: string;
  reviewedBy: string | null;
  reviewedAt: string | null;
  reviewNotes: string | null;
  receiptFileName: string | null;
  receiptFileUrl: string | null;
  // Set when an Approved payment is voided (status becomes the terminal "Voided").
  voidedBy: string | null;
  voidedAt: string | null;
  voidReason: string | null;
}

export interface BalanceInfo {
  totalFees: number;
  totalPaid: number;
  balance: number;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

export interface WorkflowDefinition {
  id: string;
  name: string;
  description: string | null;
  isActive: boolean;
  steps: WorkflowStep[];
}

export interface WorkflowStep {
  id: string;
  stepOrder: number;
  stepName: string;
  fromStatus: string;
  toStatus: string;
  requiredRole: string | null;
  requiresApproval: boolean;
}

export interface ApplicantData {
  firstName: string;
  middleName: string | null;
  lastName: string;
  email: string;
  contactNumber: string | null;
  gender: string;
  dateOfBirth: string;
  address: string | null;
  gradeLevel: string;
  schoolYear: string;
  previousSchool: string | null;
  previousSchoolAddress: string | null;
  guardianName: string | null;
  guardianContact: string | null;
  guardianRelationship: string | null;
  customFieldValues: Record<string, string | null> | null;
  lrn: string | null;
  learner: LearnerProfile;
  learnerType: string | null;
  lastGradeLevelCompleted: string | null;
  lastSchoolYearCompleted: string | null;
  lastSchoolId: string | null;
  track: string | null;
  strand: string | null;
}

export type FormFieldType = 'Text' | 'TextArea' | 'Number' | 'Date' | 'Checkbox' | 'Dropdown';
export type FormFieldSection = 'Parent' | 'Student' | 'Enrollment' | 'Guardian' | 'Family';
export type FormFieldAppliesTo = 'Both' | 'ParentMode' | 'StudentMode';

export interface Tenant {
  id: string;
  name: string;
  subdomain: string;
  contactEmail: string | null;
  contactPhone: string | null;
  address: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface PublicTenant {
  id: string;
  name: string;
  subdomain: string;
}

export interface CreateTenantRequest {
  name: string;
  subdomain: string;
  contactEmail: string | null;
  contactPhone: string | null;
  address: string | null;
}

export interface UpdateTenantRequest {
  name: string;
  subdomain: string;
  contactEmail: string | null;
  contactPhone: string | null;
  address: string | null;
  isActive: boolean;
}

export interface TenantUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  role: 'Admin' | 'Registrar' | 'SuperAdmin';
  isActive: boolean;
  createdAt: string;
}

export interface CreateTenantUserRequest {
  email: string;
  firstName: string;
  lastName: string;
  password: string;
}

export interface UpdateTenantUserRequest {
  firstName: string;
  lastName: string;
  isActive: boolean;
}

export interface Registrar {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  isActive: boolean;
  createdAt: string;
}

export interface CreateRegistrarRequest {
  email: string;
  firstName: string;
  lastName: string;
  password: string;
}

export interface UpdateRegistrarRequest {
  firstName: string;
  lastName: string;
  isActive: boolean;
}

export interface ApplicationFormField {
  id: string;
  fieldKey: string;
  label: string;
  fieldType: FormFieldType;
  section: FormFieldSection;
  appliesTo: FormFieldAppliesTo;
  isRequired: boolean;
  isVisible: boolean;
  isBuiltIn: boolean;
  /** Core fields are part of the data model — admins can rename them but can't hide them
   *  or make them optional. Visibility/required toggles are disabled in the UI. */
  isCore: boolean;
  displayOrder: number;
  options: string | null;       // JSON-encoded array of strings for Dropdown
  helpText: string | null;
}

export interface SubmitApplicationRequest {
  // "Parent" or "Student" — chosen on the /apply form (anonymous applicants).
  // Authenticated parents adding another child can leave as "Parent" (it's forced server-side).
  applicationType: 'Parent' | 'Student';
  // Parent fields — required when applicationType="Parent" and applicant is anonymous; otherwise ignored.
  parentFirstName: string | null;
  parentLastName: string | null;
  parentEmail: string | null;
  parentContactNumber: string | null;
  // One or more applicants. Parent mode: 1+ children. Student mode: exactly one (the student themselves).
  applicants: ApplicantData[];
}

export interface ParentChild {
  studentId: string | null;
  applicationId: string | null;
  firstName: string;
  middleName: string | null;
  lastName: string;
  fullName: string;
  gradeLevel: string | null;
  schoolYear: string | null;
  status: string | null;
  source: 'Application' | 'Student';
  // False when the child's latest enrollment is for a past school year —
  // the dashboard then offers re-enrollment instead of showing a stale status as current.
  hasActiveYearEnrollment: boolean;
}

export interface ApplicationListDto {
  id: string;
  applicationNumber: string;
  fullName: string;
  email: string;
  gradeLevel: string;
  schoolYear: string;
  status: string;
  createdAt: string;
  reviewedAt: string | null;
}

export interface ApplicationDetailDto {
  id: string;
  applicationNumber: string;
  firstName: string;
  middleName: string | null;
  lastName: string;
  email: string;
  contactNumber: string | null;
  gender: string;
  dateOfBirth: string;
  address: string | null;
  gradeLevel: string;
  schoolYear: string;
  previousSchool: string | null;
  previousSchoolAddress: string | null;
  guardianName: string | null;
  guardianContact: string | null;
  guardianRelationship: string | null;
  status: string;
  createdAt: string;
  reviewedAt: string | null;
  reviewNotes: string | null;
  studentId: string | null;
  applicationType: 'Parent' | 'Student';
  parentFirstName: string | null;
  parentLastName: string | null;
  parentEmail: string | null;
  parentContactNumber: string | null;
  customFieldValues: Record<string, string | null> | null;
  lrn?: string | null;
  learner?: LearnerProfile | null;
  enrollmentInfo?: EnrollmentLearnerInfo | null;
}

export interface ParentPayRequest {
  amount: number;
  paymentMethod: string;
  referenceNumber: string | null;
  remarks: string | null;
  receiptFileName?: string | null;
  receiptFileUrl?: string | null;
}

export interface StudentPayRequest {
  amount: number;
  paymentMethod: string;
  referenceNumber: string | null;
  remarks: string | null;
  receiptFileName?: string | null;
  receiptFileUrl?: string | null;
}

export interface ApplicationStatusDto {
  applicationNumber: string;
  applicantName: string;
  gradeLevel: string;
  schoolYear: string;
  status: string;
  submittedAt: string;
  reviewedAt: string | null;
  reviewNotes: string | null;
}

export interface MyPaymentsResponse {
  balance: BalanceInfo;
  payments: Payment[];
  paymentPlan: string | null;
  fees: FeeLine[];
  schedule: Installment[];
  discountAmount: number | null;
  interestAmount: number | null;
  // The school year this response describes (endpoints accept ?schoolYear= to view past years).
  schoolYear: string;
  // Other years with activity, so the UI can flag outstanding balances from prior years.
  otherYears: { schoolYear: string; balance: number }[];
}

export interface PaymentTerm {
  id: string;
  schoolYear: string;
  planType: string;
  downPaymentPercent: number;
  interestRatePercent: number;
  discountPercent: number;
  installmentCount: number;
  isActive: boolean;
}

export interface FeeLine {
  name: string;
  description: string | null;
  amount: number;
}

export interface Installment {
  number: number;
  label: string;
  amount: number;
  dueDate: string;
  isPaid: boolean;
}

export interface SchoolYear {
  id: string;
  name: string;
  startDate: string;
  endDate: string;
  isActive: boolean;
  createdAt: string;
}

export interface RequirementTemplate {
  id: string;
  documentName: string;
  gradeLevel: string | null;
  isActive: boolean;
  displayOrder: number;
  keepOnFile: boolean;
}

// Cashier's collections journal (GET /reports/collections). Rows ascending by
// paymentDate; summary covers the whole filtered range regardless of paging.
export interface CollectionRow {
  paymentId: string;
  paymentDate: string;
  studentName: string;
  gradeLevel: string;
  schoolYear: string;
  amount: number;
  paymentMethod: string;
  referenceNumber: string | null;
  receivedBy: string | null;
  enrollmentId: string;
}

export interface CollectionsMethodSummary {
  method: string;
  amount: number;
  count: number;
}

export interface CollectionsDaySummary {
  date: string;
  amount: number;
  count: number;
}

export interface CollectionsReport {
  rows: CollectionRow[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  summary: {
    totalAmount: number;
    totalCount: number;
    byMethod: CollectionsMethodSummary[];
    byDay: CollectionsDaySummary[];
  };
}

// Enrollment register (GET /reports/enrollment-register): one school year, deterministic
// order (grade, name). Summary covers the whole filtered set regardless of paging.
// Money is null before assessment — never a faked zero.
export interface EnrollmentRegisterRow {
  enrollmentId: string;
  studentId: string;
  lrn: string | null;
  studentNumber: string;
  studentName: string;
  gradeLevel: string;
  sectionName: string | null;
  status: string;
  paymentPlan: string | null;
  assessedTotal: number | null;
  totalPaid: number;
  balance: number | null;
  createdAt: string;
}

export interface EnrollmentRegister {
  items: EnrollmentRegisterRow[];
  totalCount: number;
  summary: {
    totalCount: number;
    byStatus: { status: string; count: number }[];
    byGrade: { gradeLevel: string; count: number }[];
  };
}

export interface EnrollmentRegisterFilters {
  schoolYear?: string;
  gradeLevel?: string;
  status?: string;
  sectionId?: string;
  includeCancelled?: boolean;
}

// Class list for one section (GET /sections/{id}/roster); cancelled enrollments excluded.
export interface SectionRoster {
  section: { id: string; name: string; gradeLevel: string; schoolYear: string; capacity: number; currentCount: number; adviser: string | null };
  students: { studentId: string; enrollmentId: string; lrn: string | null; studentNumber: string; name: string; gender: string | null; status: string }[];
}

export interface DashboardStats {
  schoolYear: string;
  totalStudents: number;
  totalEnrollments: number;
  pendingApplications: number;
  draftEnrollments: number;
  approvedEnrollments: number;
  enrolledCount: number;
  totalSections: number;
  totalRevenue: number;
  pendingPayments: number;
}
