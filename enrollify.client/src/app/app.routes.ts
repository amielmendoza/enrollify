import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';

// Route order matters: literal top-level paths (login, apply, tenants) are matched first so
// they aren't shadowed by the slug-based per-school route. The subdomain validator on the
// server rejects any of these literal names from being used as a school's subdomain, which
// keeps the two route families from ever colliding.
export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./pages/login/login.component').then(m => m.LoginComponent) },

  // Public school directory.
  { path: 'tenants', loadComponent: () => import('./pages/tenants-directory/tenants-directory.component').then(m => m.TenantsDirectoryComponent) },

  // Bare /apply: authenticated parents adding another child resolve their tenant from auth;
  // anonymous visitors are bounced to /tenants by the apply component.
  { path: 'apply', loadComponent: () => import('./pages/apply/apply.component').then(m => m.ApplyComponent) },

  // Printable documents (COR / assessment slip / official receipt), ?doc=cor|assessment|receipt&paymentId=...
  // Deliberately top-level, outside MainLayout, so the sidebar/nav never ends up on paper.
  { path: 'print/enrollment/:id', canActivate: [authGuard, roleGuard], data: { roles: ['Admin', 'Registrar'] }, loadComponent: () => import('./pages/print/print-enrollment.component').then(m => m.PrintEnrollmentComponent) },

  // Per-school slug-based apply page (e.g. /mshs/apply, /qcshs/apply).
  // Sits before the auth-protected MainLayout so it's reachable without a login session.
  { path: ':slug/apply', loadComponent: () => import('./pages/apply/apply.component').then(m => m.ApplyComponent) },

  // Anonymous application-status lookup by application number (e.g. /mshs/status).
  { path: ':slug/status', loadComponent: () => import('./pages/application-status/application-status.component').then(m => m.ApplicationStatusComponent) },

  {
    path: '',
    loadComponent: () => import('./layout/main-layout/main-layout.component').then(m => m.MainLayoutComponent),
    canActivate: [authGuard],
    children: [
      { path: 'dashboard', loadComponent: () => import('./pages/dashboard/dashboard.component').then(m => m.DashboardComponent) },
      { path: 'students', canActivate: [roleGuard], data: { roles: ['Admin', 'Registrar'] }, loadComponent: () => import('./pages/students/student-list/student-list.component').then(m => m.StudentListComponent) },
      { path: 'students/new', canActivate: [roleGuard], data: { roles: ['Admin', 'Registrar'] }, loadComponent: () => import('./pages/students/student-form/student-form.component').then(m => m.StudentFormComponent) },
      { path: 'students/:id/edit', canActivate: [roleGuard], data: { roles: ['Admin', 'Registrar'] }, loadComponent: () => import('./pages/students/student-form/student-form.component').then(m => m.StudentFormComponent) },
      { path: 'enrollments', canActivate: [roleGuard], data: { roles: ['Admin', 'Registrar'] }, loadComponent: () => import('./pages/enrollments/enrollment-list/enrollment-list.component').then(m => m.EnrollmentListComponent) },
      { path: 'enrollments/new', canActivate: [roleGuard], data: { roles: ['Admin', 'Registrar'] }, loadComponent: () => import('./pages/enrollments/enrollment-wizard/enrollment-wizard.component').then(m => m.EnrollmentWizardComponent) },
      { path: 'enrollments/:id', canActivate: [roleGuard], data: { roles: ['Admin', 'Registrar'] }, loadComponent: () => import('./pages/enrollments/enrollment-detail/enrollment-detail.component').then(m => m.EnrollmentDetailComponent) },
      { path: 'admissions', canActivate: [roleGuard], data: { roles: ['Admin', 'Registrar'] }, loadComponent: () => import('./pages/admissions/admissions.component').then(m => m.AdmissionsComponent) },
      { path: 'settings', canActivate: [roleGuard], data: { roles: ['Admin', 'Registrar'] }, loadComponent: () => import('./pages/settings/settings.component').then(m => m.SettingsComponent) },

      // Student-self pages
      { path: 'my-enrollment', canActivate: [roleGuard], data: { roles: ['Student'] }, loadComponent: () => import('./pages/student/my-enrollment.component').then(m => m.MyEnrollmentComponent) },
      { path: 'my-payments', canActivate: [roleGuard], data: { roles: ['Student'] }, loadComponent: () => import('./pages/student/my-payments.component').then(m => m.MyPaymentsComponent) },
      { path: 'my-profile', canActivate: [roleGuard], data: { roles: ['Student'] }, loadComponent: () => import('./pages/student/my-profile.component').then(m => m.MyProfileComponent) },

      // Parent pages (multi-child)
      { path: 'parent/dashboard', canActivate: [roleGuard], data: { roles: ['Parent'] }, loadComponent: () => import('./pages/parent/parent-dashboard/parent-dashboard.component').then(m => m.ParentDashboardComponent) },
      { path: 'parent/children/:id/enrollment', canActivate: [roleGuard], data: { roles: ['Parent'] }, loadComponent: () => import('./pages/parent/child-enrollment/child-enrollment.component').then(m => m.ChildEnrollmentComponent) },
      { path: 'parent/children/:id/payments', canActivate: [roleGuard], data: { roles: ['Parent'] }, loadComponent: () => import('./pages/parent/child-payments/child-payments.component').then(m => m.ChildPaymentsComponent) },
      { path: 'parent/children/:id/profile', canActivate: [roleGuard], data: { roles: ['Parent'] }, loadComponent: () => import('./pages/parent/child-profile/child-profile.component').then(m => m.ChildProfileComponent) },

      // SuperAdmin: school management
      { path: 'super/tenants', canActivate: [roleGuard], data: { roles: ['SuperAdmin'] }, loadComponent: () => import('./pages/super-tenants/super-tenants.component').then(m => m.SuperTenantsComponent) },
      { path: 'super/tenants/:tenantId/admins', canActivate: [roleGuard], data: { roles: ['SuperAdmin'] }, loadComponent: () => import('./pages/super-tenants/tenant-admins.component').then(m => m.TenantAdminsComponent) },

      { path: '', redirectTo: 'dashboard', pathMatch: 'full' }
    ]
  },
  { path: '**', redirectTo: 'login' }
];
