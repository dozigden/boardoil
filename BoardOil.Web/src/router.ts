import { createRouter, createWebHistory } from 'vue-router';
import type { RouteRecordRaw } from 'vue-router';
import { resolveAuthNavigation } from './site/auth/navigationGuard';
import { useAuthStore } from './shared/stores/authStore';
import {
  APP_LAYOUT_ADMIN,
  APP_LAYOUT_BOARD_ADMIN,
  APP_LAYOUT_BOARD_WITH_CONVEYOR,
  APP_LAYOUT_STANDARD
} from './site/layouts/appLayout';

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'login',
    component: () => import('./site/views/LoginView.vue'),
    meta: { requiresAuth: false, layout: APP_LAYOUT_STANDARD }
  },
  {
    path: '/setup-initial-admin',
    name: 'setup-initial-admin',
    component: () => import('./site/views/SetupInitialAdminView.vue'),
    meta: { requiresAuth: false, layout: APP_LAYOUT_STANDARD }
  },
  {
    path: '/unauthorized',
    name: 'unauthorized',
    component: () => import('./site/views/UnauthorizedView.vue'),
    meta: { requiresAuth: false, layout: APP_LAYOUT_STANDARD }
  },
  {
    path: '/licences',
    name: 'licences',
    component: () => import('./site/views/LicencesView.vue'),
    meta: { requiresAuth: false, layout: APP_LAYOUT_STANDARD }
  },
  {
    path: '/user-admin',
    component: () => import('./site/views/UserAdminView.vue'),
    meta: { requiresAuth: true, layout: APP_LAYOUT_ADMIN },
    children: [
      {
        path: '',
        redirect: { name: 'user-admin-profile' }
      },
      {
        path: 'profile',
        name: 'user-admin-profile',
        component: () => import('./site/views/UserProfileView.vue')
      },
      {
        path: 'theme',
        name: 'user-admin-theme',
        component: () => import('./site/views/UserThemeView.vue')
      },
      {
        path: 'authentication',
        component: () => import('./site/views/UserAuthenticationView.vue'),
        children: [
          {
            path: '',
            redirect: { name: 'user-admin-oauth-connections' }
          },
          {
            path: 'oauth',
            name: 'user-admin-oauth-connections',
            component: () => import('./shared/views/OAuthConnectionsView.vue')
          },
          {
            path: 'access-tokens',
            name: 'user-admin-access-tokens',
            component: () => import('./site/views/AccessTokensView.vue')
          },
          {
            path: 'mcp-help',
            name: 'user-admin-mcp-help',
            component: () => import('./site/views/McpHelpView.vue')
          }
        ]
      },
      {
        path: 'reset-password',
        name: 'user-admin-reset-password',
        component: () => import('./site/views/UserResetPasswordView.vue')
      }
    ]
  },
  {
    path: '/',
    name: 'boards',
    component: () => import('./board/views/BoardsView.vue'),
    meta: { requiresAuth: true, layout: APP_LAYOUT_STANDARD }
  },
  {
    path: '/boards/:boardId(\\d+)',
    name: 'board',
    component: () => import('./board/views/BoardView.vue'),
    meta: { requiresAuth: true, requiresBoardContext: true, layout: APP_LAYOUT_BOARD_WITH_CONVEYOR }
  },
  {
    path: '/boards/:boardId(\\d+)/archived',
    name: 'board-archived',
    component: () => import('./board/views/ArchivedCardsView.vue'),
    meta: { requiresAuth: true, requiresBoardContext: true, layout: APP_LAYOUT_BOARD_WITH_CONVEYOR }
  },
  {
    path: '/boards/:boardId(\\d+)/card/:cardId(\\d+)',
    name: 'board-card',
    components: {
      default: () => import('./board/views/BoardView.vue'),
      dialog: () => import('./board/components/CardEditorDialog.vue')
    },
    meta: { requiresAuth: true, requiresBoardContext: true, layout: APP_LAYOUT_BOARD_WITH_CONVEYOR }
  },
  {
    path: '/boards/:boardId(\\d+)/admin',
    component: () => import('./board/views/BoardAdminView.vue'),
    meta: { requiresAuth: true, requiresBoardContext: true, layout: APP_LAYOUT_BOARD_ADMIN },
    children: [
      {
        path: '',
        name: 'board-admin',
        redirect: to => ({ name: 'board-details', params: { boardId: to.params.boardId } })
      },
      {
        path: 'details',
        name: 'board-details',
        component: () => import('./board/views/BoardDetailsView.vue')
      },
      {
        path: 'columns',
        name: 'columns',
        component: () => import('./board/views/ColumnsManagerView.vue')
      },
      {
        path: 'columns/:columnId(\\d+)',
        name: 'columns-column',
        components: {
          default: () => import('./board/views/ColumnsManagerView.vue'),
          dialog: () => import('./board/components/ColumnEditorDialog.vue')
        }
      },
      {
        path: 'tags',
        name: 'tags',
        component: () => import('./board/views/TagsManagerView.vue')
      },
      {
        path: 'tags/new',
        name: 'tags-new',
        components: {
          default: () => import('./board/views/TagsManagerView.vue'),
          dialog: () => import('./board/components/TagEditorDialog.vue')
        }
      },
      {
        path: 'tags/:tagId(\\d+)',
        name: 'tags-tag',
        components: {
          default: () => import('./board/views/TagsManagerView.vue'),
          dialog: () => import('./board/components/TagEditorDialog.vue')
        }
      },
      {
        path: 'slicks',
        name: 'slicks',
        component: () => import('./board/views/SlicksManagerView.vue')
      },
      {
        path: 'slicks/new',
        name: 'slicks-new',
        components: {
          default: () => import('./board/views/SlicksManagerView.vue'),
          dialog: () => import('./board/components/SlickEditorDialog.vue')
        }
      },
      {
        path: 'slicks/:slickId(\\d+)',
        name: 'slicks-slick',
        components: {
          default: () => import('./board/views/SlicksManagerView.vue'),
          dialog: () => import('./board/components/SlickEditorDialog.vue')
        }
      },
      {
        path: 'card-types',
        name: 'card-types',
        component: () => import('./board/views/CardTypesManagerView.vue')
      },
      {
        path: 'card-types/new',
        name: 'card-types-new',
        components: {
          default: () => import('./board/views/CardTypesManagerView.vue'),
          dialog: () => import('./board/components/CardTypeEditorDialog.vue')
        }
      },
      {
        path: 'card-types/:cardTypeId(\\d+)',
        name: 'card-types-card-type',
        components: {
          default: () => import('./board/views/CardTypesManagerView.vue'),
          dialog: () => import('./board/components/CardTypeEditorDialog.vue')
        }
      },
      {
        path: 'members',
        name: 'board-members',
        component: () => import('./board/views/BoardMembersView.vue')
      },
      {
        path: 'delete',
        name: 'board-delete',
        component: () => import('./board/views/BoardDeleteView.vue')
      }
    ]
  },
  {
    path: '/admin/system',
    component: () => import('./system/views/SystemAdminView.vue'),
    meta: { requiresAuth: true, requiresAdmin: true, layout: APP_LAYOUT_ADMIN },
    children: [
      {
        path: '',
        redirect: { name: 'system-admin-boards' }
      },
      {
        path: 'boards',
        name: 'system-admin-boards',
        component: () => import('./system/views/SystemBoardsView.vue')
      },
      {
        path: 'boards/:boardId(\\d+)/members',
        name: 'system-admin-board-members',
        component: () => import('./system/views/SystemBoardMembersView.vue')
      },
      {
        path: 'users',
        name: 'users',
        component: () => import('./system/views/UsersManagerView.vue')
      },
      {
        path: 'client-accounts',
        name: 'client-accounts',
        component: () => import('./system/views/ClientAccountsView.vue')
      },
      {
        path: 'client-accounts/:clientAccountId(\\d+)/tokens',
        name: 'client-account-tokens',
        component: () => import('./system/views/ClientAccountTokensView.vue')
      },
      {
        path: 'oauth-connections',
        name: 'system-admin-oauth-connections',
        component: () => import('./shared/views/OAuthConnectionsView.vue'),
        props: { administrator: true }
      },
      {
        path: 'oauth-logs',
        name: 'system-oauth-logs',
        component: () => import('./system/views/OAuthDiagnosticsView.vue')
      },
      {
        path: 'configuration',
        name: 'configuration',
        component: () => import('./system/views/ConfigurationView.vue')
      },
      {
        path: 'system-info-message',
        name: 'system-info-message',
        component: () => import('./system/views/SystemInfoMessageView.vue')
      },
      {
        path: 'error-logs',
        name: 'system-error-logs',
        component: () => import('./system/views/ErrorLogsManagerView.vue')
      },
      {
        path: 'error-logs/:errorLogId(\\d+)',
        name: 'system-error-log-details',
        components: {
          default: () => import('./system/views/ErrorLogsManagerView.vue'),
          dialog: () => import('./system/components/ErrorLogDetailsDialogRoute.vue')
        }
      }
    ]
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: '/'
  }
];

export const router = createRouter({
  history: createWebHistory(),
  routes
});

router.beforeEach(async to => {
  const authStore = useAuthStore();
  return resolveAuthNavigation(to, authStore);
});
