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

export interface CreateStudentAccountRequest {
  email: string;
  password: string;
}

export interface SubmitApplicationRequest {
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
}

export interface StudentPayRequest {
  amount: number;
  paymentMethod: string;
  referenceNumber: string | null;
  remarks: string | null;
}

export interface MyPaymentsResponse {
  balance: BalanceInfo;
  payments: Payment[];
}
