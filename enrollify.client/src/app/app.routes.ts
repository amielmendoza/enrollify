import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./pages/login/login.component').then(m => m.LoginComponent) },
  { path: 'apply', loadComponent: () => import('./pages/apply/apply.component').then(m => m.ApplyComponent) },
  {
    path: '',
    loadComponent: () => import('./layout/main-layout/main-layout.component').then(m => m.MainLayoutComponent),
    canActivate: [authGuard],
    children: [
      { path: 'dashboard', loadComponent: () => import('./pages/dashboard/dashboard.component').then(m => m.DashboardComponent) },
      { path: 'students', loadComponent: () => import('./pages/students/student-list/student-list.component').then(m => m.StudentListComponent) },
      { path: 'students/new', loadComponent: () => import('./pages/students/student-form/student-form.component').then(m => m.StudentFormComponent) },
      { path: 'students/:id/edit', loadComponent: () => import('./pages/students/student-form/student-form.component').then(m => m.StudentFormComponent) },
      { path: 'enrollments', loadComponent: () => import('./pages/enrollments/enrollment-list/enrollment-list.component').then(m => m.EnrollmentListComponent) },
      { path: 'enrollments/new', loadComponent: () => import('./pages/enrollments/enrollment-wizard/enrollment-wizard.component').then(m => m.EnrollmentWizardComponent) },
      { path: 'enrollments/:id', loadComponent: () => import('./pages/enrollments/enrollment-detail/enrollment-detail.component').then(m => m.EnrollmentDetailComponent) },
      { path: 'admissions', loadComponent: () => import('./pages/admissions/admissions.component').then(m => m.AdmissionsComponent) },
      { path: 'settings', loadComponent: () => import('./pages/settings/settings.component').then(m => m.SettingsComponent) },
      { path: 'my-enrollment', loadComponent: () => import('./pages/student/my-enrollment/my-enrollment.component').then(m => m.MyEnrollmentComponent) },
      { path: 'my-payments', loadComponent: () => import('./pages/student/my-payments/my-payments.component').then(m => m.MyPaymentsComponent) },
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' }
    ]
  },
  { path: '**', redirectTo: 'login' }
];
