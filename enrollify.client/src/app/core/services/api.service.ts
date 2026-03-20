import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Student, CreateStudentRequest, Enrollment, CreateEnrollmentRequest,
  Section, Fee, Payment, BalanceInfo, PagedResult, WorkflowDefinition,
  CreateStudentAccountRequest, SubmitApplicationRequest, ApplicationListDto, ApplicationDetailDto,
  StudentPayRequest, MyPaymentsResponse
} from '../models';

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

  assignSection(enrollmentId: string, sectionId: string): Observable<Enrollment> {
    return this.http.post<Enrollment>(`${this.baseUrl}/enrollments/${enrollmentId}/assign-section`, { sectionId });
  }

  // Sections
  getSections(schoolYear?: string, gradeLevel?: string): Observable<Section[]> {
    let params = new HttpParams();
    if (schoolYear) params = params.set('schoolYear', schoolYear);
    if (gradeLevel) params = params.set('gradeLevel', gradeLevel);
    return this.http.get<Section[]>(`${this.baseUrl}/sections`, { params });
  }

  createSection(data: Record<string, unknown>): Observable<Section> {
    return this.http.post<Section>(`${this.baseUrl}/sections`, data);
  }

  // Fees
  getFees(schoolYear?: string, gradeLevel?: string): Observable<Fee[]> {
    let params = new HttpParams();
    if (schoolYear) params = params.set('schoolYear', schoolYear);
    if (gradeLevel) params = params.set('gradeLevel', gradeLevel);
    return this.http.get<Fee[]>(`${this.baseUrl}/fees`, { params });
  }

  createFee(data: Record<string, unknown>): Observable<Fee> {
    return this.http.post<Fee>(`${this.baseUrl}/fees`, data);
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

  // Student self-service
  getMyProfile(): Observable<Student> {
    return this.http.get<Student>(`${this.baseUrl}/students/me`);
  }

  getMyEnrollments(): Observable<Enrollment[]> {
    return this.http.get<Enrollment[]>(`${this.baseUrl}/enrollments/me`);
  }

  createStudentAccount(studentId: string, data: CreateStudentAccountRequest): Observable<any> {
    return this.http.post(`${this.baseUrl}/students/${studentId}/create-account`, data);
  }

  // Admissions (public)
  submitApplication(data: SubmitApplicationRequest): Observable<ApplicationDetailDto> {
    return this.http.post<ApplicationDetailDto>(`${this.baseUrl}/admissions/apply`, data);
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

  // Student self-service
  requestEnrollment(): Observable<Enrollment> {
    return this.http.post<Enrollment>(`${this.baseUrl}/enrollments/request`, {});
  }

  submitStudentPayment(data: StudentPayRequest): Observable<Payment> {
    return this.http.post<Payment>(`${this.baseUrl}/payments/my`, data);
  }

  getMyPaymentsAndBalance(): Observable<MyPaymentsResponse> {
    return this.http.get<MyPaymentsResponse>(`${this.baseUrl}/payments/my`);
  }

  // Student enrollment actions
  submitEnrollment(enrollmentId: string): Observable<Enrollment> {
    return this.http.post<Enrollment>(`${this.baseUrl}/enrollments/${enrollmentId}/submit`, {});
  }

  uploadRequirement(requirementId: string, fileName: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/enrollments/requirements/${requirementId}/upload`, { requirementId, fileName });
  }

  selectPaymentPlan(plan: string): Observable<any> {
    return this.http.post(`${this.baseUrl}/enrollments/payment-plan`, { paymentPlan: plan });
  }
}
