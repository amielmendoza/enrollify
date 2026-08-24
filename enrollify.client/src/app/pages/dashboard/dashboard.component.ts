import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, Router } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { SchoolYearService } from '../../core/services/school-year.service';
import { NotificationService } from '../../core/services/notification.service';
import { Enrollment, BalanceInfo, DashboardStats } from '../../core/models';
import { ENROLLMENT_STATUS_NAMES } from '../../core/constants';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div>
      <div class="flex items-start justify-between">
        <div>
          <h1 class="text-2xl font-bold text-gray-900">Good day, {{ firstName() }} \u{1F44B}</h1>
          <p class="mt-1 text-sm text-gray-500">
            {{ isStudent() ? "Here's your enrollment overview" : 'Admin overview' }} for S.Y. {{ syService.activeName() || '---' }}
          </p>
        </div>
      </div>

      @if (isStudent()) {
        <!-- STUDENT DASHBOARD -->

        <!-- No enrollment yet - Request one -->
        @if (!myEnrollment()) {
          <div class="bg-white rounded-xl border border-gray-200 p-6 mt-6">
            <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
              <div>
                <p class="text-sm text-gray-400">Get started</p>
                <h2 class="text-lg font-semibold text-gray-900 mt-1">Request your enrollment</h2>
                <p class="text-sm text-gray-500 mt-1">Submit an enrollment request for the current school year</p>
              </div>
              <button (click)="requestEnrollment()"
                 [disabled]="requesting()"
                 class="bg-[#4361ee] text-white rounded-xl px-6 py-3 text-sm font-medium hover:bg-[#3a56d4] transition-colors whitespace-nowrap disabled:opacity-60">
                {{ requesting() ? 'Requesting...' : 'Request Enrollment' }}
              </button>
            </div>
          </div>
        }

        <!-- Next Step Card (hide when enrolled) -->
        @if (myEnrollment() && !isEnrolled()) {
          <div class="bg-white rounded-xl border border-gray-200 p-6 mt-6">
            <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
              <div>
                <p class="text-sm text-gray-400">Next step</p>
                <h2 class="text-lg font-semibold text-gray-900 mt-1">Complete your enrollment</h2>
                <p class="text-sm text-gray-500 mt-1">Current status: <span class="font-medium">{{ statusName() }}</span></p>
              </div>
              <a routerLink="/my-enrollment"
                 class="bg-[#4361ee] text-white rounded-xl px-6 py-3 text-sm font-medium hover:bg-[#3a56d4] transition-colors whitespace-nowrap text-center">
                View Enrollment &rarr;
              </a>
            </div>
          </div>
        }

        <!-- Enrolled Success Card -->
        @if (isEnrolled()) {
          <div class="bg-white rounded-xl border border-emerald-200 p-6 mt-6">
            <div class="flex items-center gap-3">
              <div class="w-10 h-10 rounded-full bg-emerald-50 flex items-center justify-center">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-emerald-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" /></svg>
              </div>
              <div>
                <h2 class="text-lg font-semibold text-gray-900">You are enrolled!</h2>
                <p class="text-sm text-gray-500">{{ myEnrollment()!.gradeLevel }} &bull; {{ myEnrollment()!.sectionName || '' }} &bull; {{ myEnrollment()!.schoolYear }}</p>
              </div>
            </div>
          </div>
        }

        <!-- Student Stats -->
        <div class="grid grid-cols-1 sm:grid-cols-3 gap-6 mt-6">
          <div class="bg-white rounded-xl border border-gray-200 p-6">
            <div class="flex items-start gap-4">
              <div class="text-[#4361ee] bg-blue-50 rounded-lg p-2">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M9 12h3.75M9 15h3.75M9 18h3.75m3 .75H18a2.25 2.25 0 0 0 2.25-2.25V6.108c0-1.135-.845-2.098-1.976-2.192a48.424 48.424 0 0 0-1.123-.08m-5.801 0c-.065.21-.1.433-.1.664 0 .414.336.75.75.75h4.5a.75.75 0 0 0 .75-.75 2.25 2.25 0 0 0-.1-.664m-5.8 0A2.251 2.251 0 0 1 13.5 2.25H15a2.25 2.25 0 0 1 2.15 1.586m-5.8 0c-.376.023-.75.05-1.124.08C9.095 4.01 8.25 4.973 8.25 6.108V8.25m0 0H4.875c-.621 0-1.125.504-1.125 1.125v11.25c0 .621.504 1.125 1.125 1.125h9.75c.621 0 1.125-.504 1.125-1.125V9.375c0-.621-.504-1.125-1.125-1.125H8.25Z" />
                </svg>
              </div>
              <div>
                <p class="text-sm text-gray-500">Enrollment Status</p>
                <p class="text-2xl font-bold text-gray-900 mt-1">{{ statusName() }}</p>
                <p class="text-sm text-gray-500 mt-1">{{ myEnrollment()?.gradeLevel || '' }}</p>
              </div>
            </div>
          </div>

          <div class="bg-white rounded-xl border border-gray-200 p-6">
            <div class="flex items-start gap-4">
              <div class="text-orange-500 bg-orange-50 rounded-lg p-2">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M2.25 8.25h19.5M2.25 9h19.5m-16.5 5.25h6m-6 2.25h3m-3.75 3h15a2.25 2.25 0 0 0 2.25-2.25V6.75A2.25 2.25 0 0 0 19.5 4.5h-15a2.25 2.25 0 0 0-2.25 2.25v10.5A2.25 2.25 0 0 0 4.5 19.5Z" />
                </svg>
              </div>
              <div>
                <p class="text-sm text-gray-500">Balance</p>
                @if (hasPendingPayments()) {
                  <p class="text-2xl font-bold text-gray-900 mt-1">\u20B1{{ myBalance()?.balance?.toLocaleString('en-PH', {minimumFractionDigits: 2}) || '0.00' }}</p>
                  <p class="text-sm text-gray-500 mt-1">outstanding</p>
                } @else {
                  <p class="text-2xl font-bold text-gray-400 mt-1">--</p>
                  <p class="text-sm text-gray-400 mt-1">No payments yet</p>
                }
              </div>
            </div>
          </div>

          <div class="bg-white rounded-xl border border-gray-200 p-6">
            <div class="flex items-start gap-4">
              <div class="text-emerald-500 bg-emerald-50 rounded-lg p-2">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M4.26 10.147a60.438 60.438 0 0 0-.491 6.347A48.62 48.62 0 0 1 12 20.904a48.62 48.62 0 0 1 8.232-4.41 60.46 60.46 0 0 0-.491-6.347m-15.482 0a50.636 50.636 0 0 0-2.658-.813A59.906 59.906 0 0 1 12 3.493a59.903 59.903 0 0 1 10.399 5.84c-.896.248-1.783.52-2.658.814m-15.482 0A50.717 50.717 0 0 1 12 13.489a50.702 50.702 0 0 1 7.74-3.342" />
                </svg>
              </div>
              <div>
                <p class="text-sm text-gray-500">Section</p>
                <p class="text-2xl font-bold text-gray-900 mt-1">{{ myEnrollment()?.sectionName || 'TBA' }}</p>
                <p class="text-sm text-gray-500 mt-1">{{ myEnrollment()?.schoolYear || '' }}</p>
              </div>
            </div>
          </div>
        </div>

        <!-- Quick Actions -->
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6 mt-6">
          <div class="bg-white rounded-xl border border-gray-200 p-6">
            <h2 class="text-lg font-semibold text-gray-900 mb-4">Quick Actions</h2>
            <div class="space-y-3">
              <a routerLink="/my-enrollment" class="border border-gray-200 rounded-lg p-3 flex items-center gap-3 hover:bg-gray-50 cursor-pointer transition-colors">
                <div class="text-[#4361ee]">
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M9 12h3.75M9 15h3.75M9 18h3.75m3 .75H18a2.25 2.25 0 0 0 2.25-2.25V6.108c0-1.135-.845-2.098-1.976-2.192a48.424 48.424 0 0 0-1.123-.08m-5.801 0c-.065.21-.1.433-.1.664 0 .414.336.75.75.75h4.5a.75.75 0 0 0 .75-.75 2.25 2.25 0 0 0-.1-.664m-5.8 0A2.251 2.251 0 0 1 13.5 2.25H15a2.25 2.25 0 0 1 2.15 1.586m-5.8 0c-.376.023-.75.05-1.124.08C9.095 4.01 8.25 4.973 8.25 6.108V8.25m0 0H4.875c-.621 0-1.125.504-1.125 1.125v11.25c0 .621.504 1.125 1.125 1.125h9.75c.621 0 1.125-.504 1.125-1.125V9.375c0-.621-.504-1.125-1.125-1.125H8.25Z" />
                  </svg>
                </div>
                <span class="text-sm font-medium text-gray-700">View Enrollment</span>
                <svg xmlns="http://www.w3.org/2000/svg" class="ml-auto h-4 w-4 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" /></svg>
              </a>
              <a routerLink="/my-payments" class="border border-gray-200 rounded-lg p-3 flex items-center gap-3 hover:bg-gray-50 cursor-pointer transition-colors">
                <div class="text-[#4361ee]">
                  <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M2.25 8.25h19.5M2.25 9h19.5m-16.5 5.25h6m-6 2.25h3m-3.75 3h15a2.25 2.25 0 0 0 2.25-2.25V6.75A2.25 2.25 0 0 0 19.5 4.5h-15a2.25 2.25 0 0 0-2.25 2.25v10.5A2.25 2.25 0 0 0 4.5 19.5Z" />
                  </svg>
                </div>
                <span class="text-sm font-medium text-gray-700">View Payments</span>
                <svg xmlns="http://www.w3.org/2000/svg" class="ml-auto h-4 w-4 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" /></svg>
              </a>
            </div>
          </div>
        </div>

      } @else {
        <!-- ADMIN DASHBOARD -->
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6 mt-6">
          <div class="bg-white rounded-xl border border-gray-200 p-6">
            <div class="flex items-start gap-4">
              <div class="text-[#4361ee] bg-blue-50 rounded-lg p-2">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5"><path stroke-linecap="round" stroke-linejoin="round" d="M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Zm8.25 2.25a2.625 2.625 0 1 1-5.25 0 2.625 2.625 0 0 1 5.25 0Z" /></svg>
              </div>
              <div><p class="text-sm text-gray-500">Total Students</p><p class="text-2xl font-bold text-gray-900 mt-1">{{ stats()?.totalStudents ?? 0 }}</p></div>
            </div>
          </div>
          <div class="bg-white rounded-xl border border-gray-200 p-6">
            <div class="flex items-start gap-4">
              <div class="text-emerald-500 bg-emerald-50 rounded-lg p-2">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12h3.75M9 15h3.75M9 18h3.75m3 .75H18a2.25 2.25 0 0 0 2.25-2.25V6.108c0-1.135-.845-2.098-1.976-2.192a48.424 48.424 0 0 0-1.123-.08m-5.801 0c-.065.21-.1.433-.1.664 0 .414.336.75.75.75h4.5a.75.75 0 0 0 .75-.75 2.25 2.25 0 0 0-.1-.664m-5.8 0A2.251 2.251 0 0 1 13.5 2.25H15a2.25 2.25 0 0 1 2.15 1.586m-5.8 0c-.376.023-.75.05-1.124.08C9.095 4.01 8.25 4.973 8.25 6.108V8.25m0 0H4.875c-.621 0-1.125.504-1.125 1.125v11.25c0 .621.504 1.125 1.125 1.125h9.75c.621 0 1.125-.504 1.125-1.125V9.375c0-.621-.504-1.125-1.125-1.125H8.25Z" /></svg>
              </div>
              <div><p class="text-sm text-gray-500">Active Enrollments</p><p class="text-2xl font-bold text-gray-900 mt-1">{{ stats()?.totalEnrollments ?? 0 }}</p></div>
            </div>
          </div>
          <div class="bg-white rounded-xl border border-gray-200 p-6">
            <div class="flex items-start gap-4">
              <div class="text-orange-500 bg-orange-50 rounded-lg p-2">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5"><path stroke-linecap="round" stroke-linejoin="round" d="M12 6v6h4.5m4.5 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" /></svg>
              </div>
              <div><p class="text-sm text-gray-500">Pending Applications</p><p class="text-2xl font-bold text-gray-900 mt-1">{{ stats()?.pendingApplications ?? 0 }}</p></div>
            </div>
          </div>
          <div class="bg-white rounded-xl border border-gray-200 p-6">
            <div class="flex items-start gap-4">
              <div class="text-violet-500 bg-violet-50 rounded-lg p-2">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5"><path stroke-linecap="round" stroke-linejoin="round" d="M12 6v12m-3-2.818.879.659c1.171.879 3.07.879 4.242 0 1.172-.879 1.172-2.303 0-3.182C13.536 12.219 12.768 12 12 12c-.725 0-1.45-.22-2.003-.659-1.106-.879-1.106-2.303 0-3.182s2.9-.879 4.006 0l.415.33M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" /></svg>
              </div>
              <div><p class="text-sm text-gray-500">Total Revenue</p><p class="text-2xl font-bold text-gray-900 mt-1">\u20B1{{ (stats()?.totalRevenue ?? 0).toLocaleString('en-PH', {minimumFractionDigits: 2}) }}</p></div>
            </div>
          </div>
        </div>

        <!-- Second row stats -->
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-6 mt-6">
          <a routerLink="/enrollments" [queryParams]="{ pendingPaymentsOnly: 'true' }"
             class="block bg-white rounded-xl border border-gray-200 p-6 hover:border-amber-300 hover:bg-amber-50/30 transition-colors">
            <div class="flex items-start gap-4">
              <div class="text-amber-500 bg-amber-50 rounded-lg p-2">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5"><path stroke-linecap="round" stroke-linejoin="round" d="M2.25 8.25h19.5M2.25 9h19.5m-16.5 5.25h6m-6 2.25h3m-3.75 3h15a2.25 2.25 0 0 0 2.25-2.25V6.75A2.25 2.25 0 0 0 19.5 4.5h-15a2.25 2.25 0 0 0-2.25 2.25v10.5A2.25 2.25 0 0 0 4.5 19.5Z" /></svg>
              </div>
              <div>
                <p class="text-sm text-gray-500">Pending Payments</p>
                <p class="text-2xl font-bold text-gray-900 mt-1">{{ stats()?.pendingPayments ?? 0 }}</p>
                <p class="text-xs text-amber-600 mt-1 font-medium">Review now &rarr;</p>
              </div>
            </div>
          </a>
          <div class="bg-white rounded-xl border border-gray-200 p-6">
            <div class="flex items-start gap-4">
              <div class="text-emerald-500 bg-emerald-50 rounded-lg p-2">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5"><path stroke-linecap="round" stroke-linejoin="round" d="M4.26 10.147a60.438 60.438 0 0 0-.491 6.347A48.62 48.62 0 0 1 12 20.904a48.62 48.62 0 0 1 8.232-4.41 60.46 60.46 0 0 0-.491-6.347m-15.482 0a50.636 50.636 0 0 0-2.658-.813A59.906 59.906 0 0 1 12 3.493a59.903 59.903 0 0 1 10.399 5.84c-.896.248-1.783.52-2.658.814m-15.482 0A50.717 50.717 0 0 1 12 13.489a50.702 50.702 0 0 1 7.74-3.342" /></svg>
              </div>
              <div><p class="text-sm text-gray-500">Enrolled Students</p><p class="text-2xl font-bold text-gray-900 mt-1">{{ stats()?.enrolledCount ?? 0 }}</p></div>
            </div>
          </div>
        </div>

        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6 mt-6">
          <div class="bg-white rounded-xl border border-gray-200 p-6">
            <h2 class="text-lg font-semibold text-gray-900 mb-4">Quick Actions</h2>
            <div class="space-y-3">
              <a routerLink="/students/new" class="border border-gray-200 rounded-lg p-3 flex items-center gap-3 hover:bg-gray-50 cursor-pointer transition-colors">
                <div class="text-[#4361ee]"><svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5"><path stroke-linecap="round" stroke-linejoin="round" d="M18 7.5v3m0 0v3m0-3h3m-3 0h-3m-2.25-4.125a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0ZM3 19.235v-.11a6.375 6.375 0 0 1 12.75 0v.109A12.318 12.318 0 0 1 9.374 21c-2.331 0-4.512-.645-6.374-1.766Z" /></svg></div>
                <span class="text-sm font-medium text-gray-700">Add New Student</span>
                <svg xmlns="http://www.w3.org/2000/svg" class="ml-auto h-4 w-4 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" /></svg>
              </a>
              <a routerLink="/enrollments/new" class="border border-gray-200 rounded-lg p-3 flex items-center gap-3 hover:bg-gray-50 cursor-pointer transition-colors">
                <div class="text-[#4361ee]"><svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5"><path stroke-linecap="round" stroke-linejoin="round" d="M12 9v6m3-3H9m12 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" /></svg></div>
                <span class="text-sm font-medium text-gray-700">New Enrollment</span>
                <svg xmlns="http://www.w3.org/2000/svg" class="ml-auto h-4 w-4 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" /></svg>
              </a>
              <a routerLink="/students" class="border border-gray-200 rounded-lg p-3 flex items-center gap-3 hover:bg-gray-50 cursor-pointer transition-colors">
                <div class="text-[#4361ee]"><svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5"><path stroke-linecap="round" stroke-linejoin="round" d="M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Zm8.25 2.25a2.625 2.625 0 1 1-5.25 0 2.625 2.625 0 0 1 5.25 0Z" /></svg></div>
                <span class="text-sm font-medium text-gray-700">View Students</span>
                <svg xmlns="http://www.w3.org/2000/svg" class="ml-auto h-4 w-4 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" /></svg>
              </a>
            </div>
          </div>
          <div class="bg-white rounded-xl border border-gray-200 p-6">
            <h2 class="text-lg font-semibold text-gray-900 mb-4">System Overview</h2>
            <div class="space-y-4">
              <div class="flex items-center justify-between"><span class="text-sm text-gray-500">Total Students</span><span class="text-sm font-semibold text-gray-900">{{ stats()?.totalStudents ?? 0 }}</span></div>
              <div class="flex items-center justify-between"><span class="text-sm text-gray-500">Active Enrollments</span><span class="text-sm font-semibold text-gray-900">{{ stats()?.totalEnrollments ?? 0 }}</span></div>
              <div class="flex items-center justify-between"><span class="text-sm text-gray-500">Enrolled Students</span><span class="text-sm font-semibold text-gray-900">{{ stats()?.enrolledCount ?? 0 }}</span></div>
              <div class="flex items-center justify-between"><span class="text-sm text-gray-500">Available Sections</span><span class="text-sm font-semibold text-gray-900">{{ stats()?.totalSections ?? 0 }}</span></div>
              <div class="flex items-center justify-between"><span class="text-sm text-gray-500">Pending Applications</span><span class="text-sm font-semibold text-gray-900">{{ stats()?.pendingApplications ?? 0 }}</span></div>
              <div class="flex items-center justify-between"><span class="text-sm text-gray-500">Enrollment Status</span><span class="inline-flex items-center rounded-full bg-green-100 px-2.5 py-0.5 text-xs font-medium text-green-700">Open</span></div>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class DashboardComponent implements OnInit {
  studentCount = signal(0);
  enrollmentCount = signal(0);
  sectionCount = signal(0);
  stats = signal<DashboardStats | null>(null);
  myEnrollment = signal<Enrollment | null>(null);
  myBalance = signal<BalanceInfo | null>(null);
  hasPendingPayments = signal(false);
  requesting = signal(false);

  private statusNames = ENROLLMENT_STATUS_NAMES;

  isStudent = computed(() => this.auth.userRole() === 'Student');
  firstName = computed(() => {
    const name = this.auth.user()?.fullName ?? 'User';
    return name.split(' ')[0];
  });
  statusName = computed(() => {
    const s = this.myEnrollment()?.status;
    if (s == null) return 'None';
    if (typeof s === 'number') return this.statusNames[s] ?? 'Unknown';
    return s;
  });
  isEnrolled = computed(() => this.statusName() === 'Enrolled');

  constructor(private api: ApiService, public auth: AuthService, public syService: SchoolYearService, private router: Router, private notify: NotificationService) {}

  ngOnInit() {
    if (this.auth.userRole() === 'Parent') {
      this.router.navigate(['/parent/dashboard']);
      return;
    }
    if (this.auth.userRole() === 'SuperAdmin') {
      this.router.navigate(['/super/tenants']);
      return;
    }
    if (this.isStudent()) {
      this.syService.ensureLoaded().subscribe(() => {
        this.api.getMyEnrollments().subscribe(enrollments => {
          const activeSY = this.syService.activeName();
          const match = enrollments.find(e => e.schoolYear === activeSY) ?? null;
          this.myEnrollment.set(match);
        });
      });
      this.api.getMyPaymentsAndBalance().subscribe({
        next: (res) => {
          this.myBalance.set(res.balance);
          this.hasPendingPayments.set(res.payments?.length > 0);
        },
        error: () => {}
      });
    } else {
      this.api.getDashboardStats().subscribe(s => this.stats.set(s));
    }
  }

  requestEnrollment() {
    this.requesting.set(true);
    this.api.requestEnrollment().subscribe({
      next: (e) => {
        this.myEnrollment.set(e);
        this.requesting.set(false);
      },
      error: (err) => {
        this.requesting.set(false);
        this.notify.error(err.error?.error || 'Failed to request enrollment');
      }
    });
  }
}
