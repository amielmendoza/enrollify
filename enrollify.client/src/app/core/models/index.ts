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
}

export interface Student {
  id: string;
  lrn: string;
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
  fullName: string;
}

export interface CreateStudentRequest {
  lrn: string;
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
}

export interface CreateEnrollmentRequest {
  studentId: string;
  schoolYear: string;
  gradeLevel: string;
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
}

export type FormFieldType = 'Text' | 'TextArea' | 'Number' | 'Date' | 'Checkbox' | 'Dropdown';
export type FormFieldSection = 'Parent' | 'Student' | 'Enrollment' | 'Guardian';
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

export interface CreateStudentAccountRequest {
  email: string;
  password: string;
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

export interface DashboardStats {
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
