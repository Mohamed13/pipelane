import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'analytics' },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login.component').then((m) => m.LoginComponent),
    data: { title: 'Sign in', breadcrumb: 'Sign in' },
  },
  {
    path: 'onboarding',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/onboarding/onboarding.component').then((m) => m.OnboardingComponent),
    data: { title: 'Onboarding', breadcrumb: 'Onboarding' },
  },
  {
    path: 'templates',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/templates/templates-list.component').then((m) => m.TemplatesListComponent),
    data: { title: 'Templates', breadcrumb: 'Templates' },
  },
  {
    path: 'contacts',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/contacts/contacts-list.component').then((m) => m.ContactsListComponent),
    data: { title: 'Contacts', breadcrumb: 'Contacts' },
  },
  {
    path: 'conversations/:contactId',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/contacts/conversation-thread.component').then(
        (m) => m.ConversationThreadComponent,
      ),
    data: {
      title: 'Conversation',
      breadcrumb: 'Conversation',
      breadcrumbTrail: [{ label: 'Contacts', url: '/contacts' }, { label: 'Conversation' }],
    },
  },
  {
    path: 'campaigns',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/campaigns/campaign-builder.component').then(
        (m) => m.CampaignBuilderComponent,
      ),
    data: { title: 'Campaign builder', breadcrumb: 'Campaign builder' },
  },
  {
    path: 'analytics',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/analytics/analytics-overview.component').then(
        (m) => m.AnalyticsOverviewComponent,
      ),
    data: { title: 'Analytics', breadcrumb: 'Analytics' },
  },
  {
    path: 'settings',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/settings/settings.component').then((m) => m.SettingsComponent),
    data: { title: 'Settings', breadcrumb: 'Settings' },
  },
  { path: '**', redirectTo: 'analytics' },
];
