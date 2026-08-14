import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Student, CreateStudentRequest, Enrollment, CreateEnrollmentRequest, EnrollmentRequirement, EnrollmentHistoryItem,
  EnrollmentLedger,
  Section, Fee, FeeLine, Payment, BalanceInfo, PagedResult, WorkflowDefinition,
  CreateStudentAccountRequest, SubmitApplicationRequest, ApplicationListDto, ApplicationDetailDto, ApplicationStatusDto,
  ParentPayRequest, StudentPayRequest, ParentChild, MyPaymentsResponse, DashboardStats, CollectionsReport, SchoolYear, PaymentTerm, RequirementTemplate,
  ApplicationFormField,
  Tenant, PublicTenant, CreateTenantRequest, UpdateTenantRequest,
  TenantUser, CreateTenantUserRequest, UpdateTenantUserRequest,
  Registrar, CreateRegistrarRequest, UpdateRegistrarRequest
} from '../models';
import { HttpHeaders } from '@angular/common/http';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private baseUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  // Students
  getStudents(search?: string, page = 1, pageSize = 20): Observable<PagedResult<Student>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    return this.http.get<PagedResult<Student>>(`${this.baseUrl}/students`, { params });
  }

  getStudent(id: string): Observable<Student> {
    return this.http.get<Student>(`${this.baseUrl}/students/${id}`);
  }

  createStudent(data: CreateStudentRequest): Observable<Student> {
    return this.http.post<Student>(`${this.baseUrl}/students`, data);
  }

  updateStudent(id: string, data: CreateStudentRequest): Observable<Student> {
    return this.http.put<Student>(`${this.baseUrl}/students/${id}`, data);
  }

  // Enrollments
  getEnrollments(filters?: { schoolYear?: string; gradeLevel?: string; status?: string; search?: string; page?: number; pageSize?: number }): Observable<PagedResult<Enrollment>> {
    let params = new HttpParams();
    if (filters) {
      if (filters.schoolYear) params = params.set('schoolYear', filters.schoolYear);
      if (filters.gradeLevel) params = params.set('gradeLevel', filters.gradeLevel);
      if (filters.status) params = params.set('status', filters.status);
      if (filters.search) params = params.set('search', filters.search);
      params = params.set('page', filters.page ?? 1).set('pageSize', filters.pageSize ?? 20);
    }
    return this.http.get<PagedResult<Enrollment>>(`${this.baseUrl}/enrollments`, { params });
  }

  getEnrollment(id: string): Observable<Enrollment> {
    return this.http.get<Enrollment>(`${this.baseUrl}/enrollments/${id}`);
  }

  createEnrollment(data: CreateEnrollmentRequest): Observable<Enrollment> {
    return this.http.post<Enrollment>(`${this.baseUrl}/enrollments`, data);
  }

  moveEnrollmentStep(id: string, remarks?: string): Observable<Enrollment> {
    return this.http.post<Enrollment>(`${this.baseUrl}/enrollments/${id}/move-step`, { remarks });
  }

  cancelEnrollment(id: string, reason?: string): Observable<Enrollment> {
    return this.http.post<Enrollment>(`${this.baseUrl}/enrollments/${id}/cancel`, { reason });
  }

  assignSection(enrollmentId: string, sectionId: string): Observable<Enrollment> {
    return this.http.post<Enrollment>(`${this.baseUrl}/enrollments/${enrollmentId}/assign-section`, { sectionId });
  }

  // Status transition history, ascending (Admin/Registrar only).
  getEnrollmentHistory(id: string): Observable<EnrollmentHistoryItem[]> {
    return this.http.get<EnrollmentHistoryItem[]>(`${this.baseUrl}/enrollments/${id}/history`);
  }

  // Assessment fee lines for an enrollment (Admin/Registrar only): snapshot from when the
  // enrollment was assessed, falling back to live active fees. Printables must use this,
  // not the live catalog, so a printed document can't contradict itself after a fee edit.
  getEnrollmentFees(id: string): Observable<FeeLine[]> {
    return this.http.get<FeeLine[]>(`${this.baseUrl}/enrollments/${id}/fees`);
  }

  // Account ledger / statement of account (Admin/Registrar). Not-yet-assessed
  // enrollments return empty entries with zero totals.
  getEnrollmentLedger(id: string): Observable<EnrollmentLedger> {
    return this.http.get<EnrollmentLedger>(`${this.baseUrl}/enrollments/${id}/ledger`);
  }

  createAdjustment(enrollmentId: string, data: { type: 'Debit' | 'Credit'; description: string; amount: number }): Observable<any> {
    return this.http.post(`${this.baseUrl}/enrollments/${enrollmentId}/adjustments`, data);
  }

  voidAdjustment(enrollmentId: string, adjustmentId: string, reason: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/enrollments/${enrollmentId}/adjustments/${adjustmentId}/void`, { reason });
  }

  // Sections. includeInactive is for admin management UIs only (Settings) — pickers
  // and assessments must stay active-only, which the default (omitted param) preserves.
  getSections(schoolYear?: string, gradeLevel?: string, includeInactive = false): Observable<Section[]> {
    let params = new HttpParams();
    if (schoolYear) params = params.set('schoolYear', schoolYear);
    if (gradeLevel) params = params.set('gradeLevel', gradeLevel);
    if (includeInactive) params = params.set('includeInactive', true);
    return this.http.get<Section[]>(`${this.baseUrl}/sections`, { params });
  }

  createSection(data: Record<string, unknown>): Observable<Section> {
    return this.http.post<Section>(`${this.baseUrl}/sections`, data);
  }

  updateSection(id: string, data: Record<string, unknown>): Observable<Section> {
    return this.http.put<Section>(`${this.baseUrl}/sections/${id}`, data);
  }

  deleteSection(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/sections/${id}`);
  }

  // Fees. Same includeInactive contract as getSections above.
  getFees(schoolYear?: string, gradeLevel?: string, includeInactive = false): Observable<Fee[]> {
    let params = new HttpParams();
    if (schoolYear) params = params.set('schoolYear', schoolYear);
    if (gradeLevel) params = params.set('gradeLevel', gradeLevel);
    if (includeInactive) params = params.set('includeInactive', true);
    return this.http.get<Fee[]>(`${this.baseUrl}/fees`, { params });
  }

  createFee(data: Record<string, unknown>): Observable<Fee> {
    return this.http.post<Fee>(`${this.baseUrl}/fees`, data);
  }

  updateFee(id: string, data: Record<string, unknown>): Observable<Fee> {
    return this.http.put<Fee>(`${this.baseUrl}/fees/${id}`, data);
  }

  deleteFee(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/fees/${id}`);
  }

  // Payments
  getPayments(enrollmentId: string): Observable<Payment[]> {
    return this.http.get<Payment[]>(`${this.baseUrl}/payments/enrollment/${enrollmentId}`);
  }

  getBalance(enrollmentId: string): Observable<BalanceInfo> {
    return this.http.get<BalanceInfo>(`${this.baseUrl}/payments/balance/${enrollmentId}`);
  }

  createPayment(data: Record<string, unknown>): Observable<Payment> {
    return this.http.post<Payment>(`${this.baseUrl}/payments`, data);
  }

  // Workflows
  getWorkflows(): Observable<WorkflowDefinition[]> {
    return this.http.get<WorkflowDefinition[]>(`${this.baseUrl}/workflows`);
  }

  // Student self-service (Role = Student)
  getMyProfile(): Observable<Student> {
    return this.http.get<Student>(`${this.baseUrl}/students/me`);
  }

  updateMyProfile(data: { contactNumber?: string; email?: string; address?: string; guardianName?: string; guardianContact?: string }): Observable<Student> {
    return this.http.put<Student>(`${this.baseUrl}/students/me`, data);
  }

  getMyEnrollments(): Observable<Enrollment[]> {
    return this.http.get<Enrollment[]>(`${this.baseUrl}/enrollments/me`);
  }

  requestEnrollment(): Observable<Enrollment> {
    return this.http.post<Enrollment>(`${this.baseUrl}/enrollments/request`, {});
  }

  submitEnrollment(enrollmentId: string): Observable<Enrollment> {
    return this.http.post<Enrollment>(`${this.baseUrl}/enrollments/${enrollmentId}/submit`, {});
  }

  uploadRequirement(requirementId: string, fileName: string, fileUrl?: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/enrollments/requirements/${requirementId}/upload`, { requirementId, fileName, fileUrl });
  }

  selectPaymentPlan(plan: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/enrollments/payment-plan`, { paymentPlan: plan });
  }

  submitStudentPayment(data: StudentPayRequest): Observable<Payment> {
    return this.http.post<Payment>(`${this.baseUrl}/payments/my`, data);
  }

  getMyPaymentsAndBalance(): Observable<MyPaymentsResponse> {
    return this.http.get<MyPaymentsResponse>(`${this.baseUrl}/payments/my`);
  }

  // Student's own account ledger (current enrollment resolved server-side).
  getMyLedger(): Observable<EnrollmentLedger> {
    return this.http.get<EnrollmentLedger>(`${this.baseUrl}/enrollments/me/ledger`);
  }

  // Admin: create a Student-role login for an existing student record (only if no parent linkage)
  createStudentAccount(studentId: string, data: CreateStudentAccountRequest): Observable<any> {
    return this.http.post(`${this.baseUrl}/students/${studentId}/create-account`, data);
  }

  // Parent: children & per-child views
  getMyChildren(): Observable<ParentChild[]> {
    return this.http.get<ParentChild[]>(`${this.baseUrl}/parent/children`);
  }

  getChildProfile(studentId: string): Observable<Student> {
    return this.http.get<Student>(`${this.baseUrl}/parent/children/${studentId}/profile`);
  }

  updateChildProfile(studentId: string, data: { contactNumber?: string; email?: string; address?: string; guardianName?: string; guardianContact?: string }): Observable<Student> {
    return this.http.put<Student>(`${this.baseUrl}/parent/children/${studentId}/profile`, data);
  }

  getChildEnrollments(studentId: string): Observable<Enrollment[]> {
    return this.http.get<Enrollment[]>(`${this.baseUrl}/parent/children/${studentId}/enrollments`);
  }

  // Admissions (public). Returns one ApplicationDetailDto per applicant in the batch.
  // Pass tenantId for anonymous applicants (so the app-level X-Tenant-Id default doesn't apply).
  submitApplication(data: SubmitApplicationRequest, tenantId?: string): Observable<ApplicationDetailDto[]> {
    const headers = tenantId ? new HttpHeaders({ 'X-Tenant-Id': tenantId }) : undefined;
    return this.http.post<ApplicationDetailDto[]>(`${this.baseUrl}/admissions/apply`, data, headers ? { headers } : {});
  }

  // Admissions (admin)
  getApplications(filters?: { status?: string; search?: string; page?: number; pageSize?: number }): Observable<PagedResult<ApplicationListDto>> {
    let params = new HttpParams();
    if (filters) {
      if (filters.status) params = params.set('status', filters.status);
      if (filters.search) params = params.set('search', filters.search);
      params = params.set('page', filters.page ?? 1).set('pageSize', filters.pageSize ?? 20);
    }
    return this.http.get<PagedResult<ApplicationListDto>>(`${this.baseUrl}/admissions`, { params });
  }

  getApplication(id: string): Observable<ApplicationDetailDto> {
    return this.http.get<ApplicationDetailDto>(`${this.baseUrl}/admissions/${id}`);
  }

  reviewApplication(id: string, isApproved: boolean, notes?: string): Observable<ApplicationDetailDto> {
    return this.http.post<ApplicationDetailDto>(`${this.baseUrl}/admissions/${id}/review`, { isApproved, notes });
  }

  reviewPayment(paymentId: string, isApproved: boolean, notes?: string): Observable<Payment> {
    return this.http.post<Payment>(`${this.baseUrl}/payments/${paymentId}/review`, { isApproved, notes });
  }

  // Parent self-service for a specific child
  requestEnrollmentForChild(studentId: string): Observable<Enrollment> {
    return this.http.post<Enrollment>(`${this.baseUrl}/parent/children/${studentId}/enrollments/request`, {});
  }

  submitParentPayment(studentId: string, data: ParentPayRequest): Observable<Payment> {
    return this.http.post<Payment>(`${this.baseUrl}/parent/children/${studentId}/payments`, data);
  }

  getChildPaymentsAndBalance(studentId: string): Observable<MyPaymentsResponse> {
    return this.http.get<MyPaymentsResponse>(`${this.baseUrl}/parent/children/${studentId}/payments`);
  }

  // A child's account ledger (current enrollment resolved server-side).
  getChildLedger(studentId: string): Observable<EnrollmentLedger> {
    return this.http.get<EnrollmentLedger>(`${this.baseUrl}/parent/children/${studentId}/ledger`);
  }

  submitChildEnrollment(studentId: string, enrollmentId: string): Observable<Enrollment> {
    return this.http.post<Enrollment>(`${this.baseUrl}/parent/children/${studentId}/enrollments/${enrollmentId}/submit`, {});
  }

  uploadChildRequirement(studentId: string, requirementId: string, fileName: string, fileUrl?: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/parent/children/${studentId}/requirements/${requirementId}/upload`, { requirementId, fileName, fileUrl });
  }

  // Admin per-enrollment requirement actions
  adminUploadRequirement(requirementId: string, fileName: string, fileUrl?: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/enrollments/requirements/${requirementId}/admin-upload`, { fileName, fileUrl });
  }

  reviewRequirement(requirementId: string, isVerified: boolean, notes?: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/enrollments/requirements/${requirementId}/review`, { isVerified, notes });
  }

  // Requirement templates (Settings)
  getRequirementTemplates(): Observable<RequirementTemplate[]> {
    return this.http.get<RequirementTemplate[]>(`${this.baseUrl}/requirementtemplates`);
  }

  createRequirementTemplate(data: { documentName: string; gradeLevel: string | null; displayOrder: number }): Observable<RequirementTemplate> {
    return this.http.post<RequirementTemplate>(`${this.baseUrl}/requirementtemplates`, data);
  }

  updateRequirementTemplate(id: string, data: { documentName: string; gradeLevel: string | null; isActive: boolean; displayOrder: number }): Observable<RequirementTemplate> {
    return this.http.put<RequirementTemplate>(`${this.baseUrl}/requirementtemplates/${id}`, data);
  }

  deleteRequirementTemplate(id: string): Observable<any> {
    return this.http.delete(`${this.baseUrl}/requirementtemplates/${id}`);
  }

  selectChildPaymentPlan(studentId: string, plan: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/parent/children/${studentId}/enrollments/payment-plan`, { paymentPlan: plan });
  }

  // File upload
  uploadFile(file: File): Observable<{ fileName: string; fileUrl: string; fileSize: number; contentType: string }> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<{ fileName: string; fileUrl: string; fileSize: number; contentType: string }>(
      `${this.baseUrl}/files/upload`, formData
    );
  }

  // Authenticated file download. GET /api/files/{id} requires the JWT (plain <a href> links
  // can't send it), so fetch as a blob and hand back an object URL the caller can open.
  downloadFile(fileUrl: string): Observable<Blob> {
    // Stored URLs are relative ("/api/files/{id}"); tolerate legacy absolute ones.
    const path = fileUrl.replace(/^https?:\/\/[^/]+/, '').replace(/^\/api/, '');
    return this.http.get(`${this.baseUrl}${path}`, { responseType: 'blob' });
  }

  openFile(fileUrl: string): void {
    this.downloadFile(fileUrl).subscribe(blob => {
      const url = URL.createObjectURL(blob);
      window.open(url, '_blank');
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    });
  }

  // Change password
  changePassword(currentPassword: string, newPassword: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/auth/change-password`, { currentPassword, newPassword });
  }

  // School Years
  getSchoolYears(): Observable<SchoolYear[]> {
    return this.http.get<SchoolYear[]>(`${this.baseUrl}/schoolyears`);
  }

  createSchoolYear(data: { name: string; startDate: string; endDate: string }): Observable<SchoolYear> {
    return this.http.post<SchoolYear>(`${this.baseUrl}/schoolyears`, data);
  }

  setActiveSchoolYear(id: string): Observable<SchoolYear> {
    return this.http.post<SchoolYear>(`${this.baseUrl}/schoolyears/${id}/set-active`, {});
  }

  deleteSchoolYear(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/schoolyears/${id}`);
  }

  // Payment Terms
  getPaymentTerms(schoolYear?: string): Observable<PaymentTerm[]> {
    let params = new HttpParams();
    if (schoolYear) params = params.set('schoolYear', schoolYear);
    return this.http.get<PaymentTerm[]>(`${this.baseUrl}/paymentterms`, { params });
  }

  savePaymentTerm(data: { schoolYear: string; planType: string; downPaymentPercent: number; interestRatePercent: number; discountPercent: number; installmentCount: number }): Observable<PaymentTerm> {
    return this.http.post<PaymentTerm>(`${this.baseUrl}/paymentterms`, data);
  }

  // Dashboard stats
  getDashboardStats(): Observable<DashboardStats> {
    return this.http.get<DashboardStats>(`${this.baseUrl}/dashboard/stats`);
  }

  // Cashier's collections journal (Admin/Registrar).
  getCollections(filters: { from: string; to: string; method?: string; page?: number; pageSize?: number }): Observable<CollectionsReport> {
    let params = new HttpParams().set('from', filters.from).set('to', filters.to);
    if (filters.method) params = params.set('method', filters.method);
    params = params.set('page', filters.page ?? 1).set('pageSize', filters.pageSize ?? 20);
    return this.http.get<CollectionsReport>(`${this.baseUrl}/reports/collections`, { params });
  }

  // CSV export of the collections journal; the auth interceptor supplies the JWT,
  // so fetch as a blob and let the caller save it via an object URL.
  exportCollections(from: string, to: string, method?: string): Observable<Blob> {
    let params = new HttpParams().set('from', from).set('to', to);
    if (method) params = params.set('method', method);
    return this.http.get(`${this.baseUrl}/reports/collections/export`, { params, responseType: 'blob' });
  }

  // Application form fields (admin)
  getApplicationFormFields(): Observable<ApplicationFormField[]> {
    return this.http.get<ApplicationFormField[]>(`${this.baseUrl}/applicationformfields`);
  }

  createApplicationFormField(data: Omit<ApplicationFormField, 'id' | 'isBuiltIn' | 'isCore'>): Observable<ApplicationFormField> {
    return this.http.post<ApplicationFormField>(`${this.baseUrl}/applicationformfields`, data);
  }

  updateApplicationFormField(id: string, data: Omit<ApplicationFormField, 'id' | 'isBuiltIn' | 'isCore'>): Observable<ApplicationFormField> {
    return this.http.put<ApplicationFormField>(`${this.baseUrl}/applicationformfields/${id}`, data);
  }

  deleteApplicationFormField(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/applicationformfields/${id}`);
  }

  restoreDefaultApplicationFormFields(): Observable<{ added: number }> {
    return this.http.post<{ added: number }>(`${this.baseUrl}/applicationformfields/restore-defaults`, {});
  }

  // Public form-field config used by /apply before login.
  getPublicApplicationFormFields(tenantId: string): Observable<ApplicationFormField[]> {
    const headers = new HttpHeaders({ 'X-Tenant-Id': tenantId });
    return this.http.get<ApplicationFormField[]>(`${this.baseUrl}/applicationformfields/public`, { headers });
  }

  // Tenants (Schools)
  getActivePublicTenants(): Observable<PublicTenant[]> {
    return this.http.get<PublicTenant[]>(`${this.baseUrl}/tenants/active`);
  }

  getPublicTenant(id: string): Observable<PublicTenant> {
    return this.http.get<PublicTenant>(`${this.baseUrl}/tenants/public/${id}`);
  }

  // SuperAdmin
  getAllTenants(activeOnly = false): Observable<Tenant[]> {
    return this.http.get<Tenant[]>(`${this.baseUrl}/tenants?activeOnly=${activeOnly}`);
  }

  createTenant(data: CreateTenantRequest): Observable<Tenant> {
    return this.http.post<Tenant>(`${this.baseUrl}/tenants`, data);
  }

  updateTenant(id: string, data: UpdateTenantRequest): Observable<Tenant> {
    return this.http.put<Tenant>(`${this.baseUrl}/tenants/${id}`, data);
  }

  // Per-tenant user management (SuperAdmin)
  getTenantUsers(tenantId: string): Observable<TenantUser[]> {
    return this.http.get<TenantUser[]>(`${this.baseUrl}/tenants/${tenantId}/users`);
  }

  createTenantUser(tenantId: string, data: CreateTenantUserRequest): Observable<TenantUser> {
    return this.http.post<TenantUser>(`${this.baseUrl}/tenants/${tenantId}/users`, data);
  }

  updateTenantUser(tenantId: string, userId: string, data: UpdateTenantUserRequest): Observable<TenantUser> {
    return this.http.put<TenantUser>(`${this.baseUrl}/tenants/${tenantId}/users/${userId}`, data);
  }

  resetTenantUserPassword(tenantId: string, userId: string, newPassword: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/tenants/${tenantId}/users/${userId}/reset-password`, { newPassword });
  }

  // Registrars (Admin only — manages registrars in own tenant)
  getRegistrars(): Observable<Registrar[]> {
    return this.http.get<Registrar[]>(`${this.baseUrl}/registrars`);
  }

  createRegistrar(data: CreateRegistrarRequest): Observable<Registrar> {
    return this.http.post<Registrar>(`${this.baseUrl}/registrars`, data);
  }

  updateRegistrar(userId: string, data: UpdateRegistrarRequest): Observable<Registrar> {
    return this.http.put<Registrar>(`${this.baseUrl}/registrars/${userId}`, data);
  }

  resetRegistrarPassword(userId: string, newPassword: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/registrars/${userId}/reset-password`, { newPassword });
  }

  // Slug-based public school endpoints — used by /tenants/:slug/apply (anonymous applicants).
  // These bypass the X-Tenant-Id header dance entirely; the slug in the URL is the source of truth.
  getSchoolBySlug(slug: string): Observable<PublicTenant> {
    return this.http.get<PublicTenant>(`${this.baseUrl}/schools/${slug}`);
  }

  getSchoolFormFields(slug: string): Observable<ApplicationFormField[]> {
    return this.http.get<ApplicationFormField[]>(`${this.baseUrl}/schools/${slug}/form-fields`);
  }

  // Anonymous school-year list for the public /apply form (the active year drives the default).
  getSchoolSchoolYears(slug: string): Observable<SchoolYear[]> {
    return this.http.get<SchoolYear[]>(`${this.baseUrl}/schools/${slug}/school-years`);
  }

  applyToSchool(slug: string, data: SubmitApplicationRequest): Observable<ApplicationDetailDto[]> {
    return this.http.post<ApplicationDetailDto[]>(`${this.baseUrl}/schools/${slug}/apply`, data);
  }

  // Anonymous application-status lookup by application number.
  getApplicationStatus(slug: string, applicationNumber: string): Observable<ApplicationStatusDto> {
    return this.http.get<ApplicationStatusDto>(
      `${this.baseUrl}/schools/${slug}/applications/${encodeURIComponent(applicationNumber)}/status`);
  }
}
