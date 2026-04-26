import { createRouter, createWebHistory } from 'vue-router';
import type { RouteRecordRaw } from 'vue-router';
import { resolveAuthNavigation } from './site/auth/navigationGuard';
import { useAuthStore } from './shared/stores/authStore';
import { APP_LAYOUT_ADMIN, APP_LAYOUT_BOARD, APP_LAYOUT_FULL_HEIGHT, APP_LAYOUT_PAGE } from './site/layouts/appLayout';

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'login',
    component: () => import('./site/views/LoginView.vue'),
    meta: { requiresAuth: false, layout: APP_LAYOUT_PAGE }
  },
  {
    path: '/setup-initial-admin',
    name: 'setup-initial-admin',
    component: () => import('./site/views/SetupInitialAdminView.vue'),
    meta: { requiresAuth: false, layout: APP_LAYOUT_PAGE }
  },
  {
    path: '/unauthorized',
    name: 'unauthorized',
    component: () => import('./site/views/UnauthorizedView.vue'),
    meta: { requiresAuth: false, layout: APP_LAYOUT_PAGE }
  },
  {
    path: '/licences',
    name: 'licences',
    component: () => import('./site/views/LicencesView.vue'),
    meta: { requiresAuth: false, layout: APP_LAYOUT_PAGE }
  },
  {
    path: '/access-tokens',
    name: 'access-tokens',
    component: () => import('./site/views/AccessTokensView.vue'),
    meta: { requiresAuth: true, layout: APP_LAYOUT_PAGE }
  },
  {
    path: '/',
    name: 'boards',
    component: () => import('./board/views/BoardsView.vue'),
    meta: { requiresAuth: true, layout: APP_LAYOUT_PAGE }
  },
  {
    path: '/boards/:boardId(\\d+)',
    name: 'board',
    component: () => import('./board/views/BoardView.vue'),
    meta: { requiresAuth: true, layout: APP_LAYOUT_BOARD }
  },
  {
    path: '/boards/:boardId(\\d+)/archived',
    name: 'board-archived',
    component: () => import('./board/views/ArchivedCardsView.vue'),
    meta: { requiresAuth: true, layout: APP_LAYOUT_FULL_HEIGHT }
  },
  {
    path: '/boards/:boardId(\\d+)/card/:cardId(\\d+)',
    name: 'board-card',
    components: {
      default: () => import('./board/views/BoardView.vue'),
      dialog: () => import('./board/components/CardEditorDialog.vue')
    },
    meta: { requiresAuth: true, layout: APP_LAYOUT_BOARD }
  },
  {
    path: '/boards/:boardId(\\d+)/admin',
    component: () => import('./board/views/BoardAdminView.vue'),
    meta: { requiresAuth: true, layout: APP_LAYOUT_ADMIN },
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
        path: 'configuration',
        name: 'configuration',
        component: () => import('./system/views/ConfigurationView.vue')
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
