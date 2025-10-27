import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'analytics' },
  {
    path: 'prospecting',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/prospecting/prospecting-dashboard.component').then(
        (m) => m.ProspectingDashboardComponent,
      ),
    data: { title: 'Prospecting', breadcrumb: 'Prospecting' },
  },
  {
    path: 'prospecting/onboarding',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/prospecting/prospecting-onboarding.component').then(
        (m) => m.ProspectingOnboardingComponent,
      ),
    data: {
      title: 'Prospecting onboarding',
      breadcrumb: 'Onboarding',
      breadcrumbTrail: [
        { label: 'Prospecting', url: '/prospecting' },
        { label: 'Onboarding' },
      ],
    },
  },
  {
    path: 'prospecting/sequences',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/prospecting/prospecting-sequences.component').then(
        (m) => m.ProspectingSequencesComponent,
      ),
    data: {
      title: 'Prospecting sequences',
      breadcrumb: 'Sequences',
      breadcrumbTrail: [
        { label: 'Prospecting', url: '/prospecting' },
        { label: 'Sequences' },
      ],
    },
  },
  {
    path: 'prospecting/campaigns/:id',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/prospecting/prospecting-campaign-detail.component').then(
        (m) => m.ProspectingCampaignDetailComponent,
      ),
    data: {
      title: 'Prospecting campaign',
      breadcrumb: 'Campaign',
      breadcrumbTrail: [
        { label: 'Prospecting', url: '/prospecting' },
        { label: 'Campaign' },
      ],
    },
  },
  {
    path: 'inbox',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/prospecting/prospecting-inbox.component').then(
        (m) => m.ProspectingInboxComponent,
      ),
    data: {
      title: 'Inbox',
      breadcrumb: 'Inbox',
      breadcrumbTrail: [{ label: 'Inbox' }],
    },
  },
  {
    path: 'prospecting/inbox',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/prospecting/prospecting-inbox.component').then(
        (m) => m.ProspectingInboxComponent,
      ),
    data: {
      title: 'Prospecting inbox',
      breadcrumb: 'Inbox',
      breadcrumbTrail: [
        { label: 'Prospecting', url: '/prospecting' },
        { label: 'Inbox' },
      ],
    },
  },
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
    path: 'hunter',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/hunter/hunter-page.component').then((m) => m.HunterPageComponent),
    data: { title: 'Lead Hunter', breadcrumb: 'Hunter' },
  },
  {
    path: 'lists',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/lists/lists-index.component').then((m) => m.ListsIndexComponent),
    data: { title: 'Lists', breadcrumb: 'Lists' },
  },
  {
    path: 'lists/:id',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/lists/list-detail.component').then((m) => m.ListDetailComponent),
    data: {
      title: 'List detail',
      breadcrumb: 'List detail',
      breadcrumbTrail: [
        { label: 'Lists', url: '/lists' },
        { label: 'Detail' },
      ],
    },
  },
  {
    path: 'settings',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/settings/settings.component').then((m) => m.SettingsComponent),
    data: { title: 'Settings', breadcrumb: 'Settings' },
  },
  {
    path: 'playground',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/style-guide/design-playground.component').then(
        (m) => m.DesignPlaygroundComponent,
      ),
    data: { title: 'Design playground', breadcrumb: 'Playground' },
  },
  { path: '**', redirectTo: 'analytics' },
];


