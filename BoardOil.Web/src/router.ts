import { createRouter, createWebHistory } from 'vue-router';
import type { RouteRecordRaw } from 'vue-router';
import { resolveAuthNavigation } from './auth/navigationGuard';
import { useAuthStore } from './stores/authStore';
import { APP_LAYOUT_ADMIN, APP_LAYOUT_BOARD, APP_LAYOUT_PAGE } from './layouts/appLayout';

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'login',
    component: () => import('./views/LoginView.vue'),
    meta: { requiresAuth: false, layout: APP_LAYOUT_PAGE }
  },
  {
    path: '/setup-initial-admin',
    name: 'setup-initial-admin',
    component: () => import('./views/SetupInitialAdminView.vue'),
    meta: { requiresAuth: false, layout: APP_LAYOUT_PAGE }
  },
  {
    path: '/unauthorized',
    name: 'unauthorized',
    component: () => import('./views/UnauthorizedView.vue'),
    meta: { requiresAuth: false, layout: APP_LAYOUT_PAGE }
  },
  {
    path: '/licences',
    name: 'licences',
    component: () => import('./views/LicencesView.vue'),
    meta: { requiresAuth: false, layout: APP_LAYOUT_PAGE }
  },
  {
    path: '/',
    name: 'boards',
    component: () => import('./views/BoardsView.vue'),
    meta: { requiresAuth: true, layout: APP_LAYOUT_PAGE }
  },
  {
    path: '/boards/:boardId(\\d+)',
    name: 'board',
    component: () => import('./views/BoardView.vue'),
    meta: { requiresAuth: true, layout: APP_LAYOUT_BOARD }
  },
  {
    path: '/boards/:boardId(\\d+)/card/:cardId(\\d+)',
    name: 'board-card',
    components: {
      default: () => import('./views/BoardView.vue'),
      dialog: () => import('./components/CardEditorDialog.vue')
    },
    meta: { requiresAuth: true, layout: APP_LAYOUT_BOARD }
  },
  {
    path: '/boards/:boardId(\\d+)/admin',
    name: 'board-admin',
    component: () => import('./views/BoardAdminView.vue'),
    meta: { requiresAuth: true, layout: APP_LAYOUT_ADMIN },
    children: [
      {
        path: '',
        redirect: to => ({ name: 'tags', params: { boardId: to.params.boardId } })
      },
      {
        path: 'columns',
        name: 'columns',
        component: () => import('./views/ColumnsManagerView.vue')
      },
      {
        path: 'columns/:columnId(\\d+)',
        name: 'columns-column',
        components: {
          default: () => import('./views/ColumnsManagerView.vue'),
          dialog: () => import('./components/ColumnEditorDialog.vue')
        }
      },
      {
        path: 'tags',
        name: 'tags',
        component: () => import('./views/TagsManagerView.vue')
      },
      {
        path: 'tags/:tagId(\\d+)',
        name: 'tags-tag',
        components: {
          default: () => import('./views/TagsManagerView.vue'),
          dialog: () => import('./components/TagEditorDialog.vue')
        }
      },
      {
        path: 'members',
        name: 'board-members',
        component: () => import('./views/BoardMembersView.vue')
      }
    ]
  },
  {
    path: '/admin/system',
    component: () => import('./views/SystemAdminView.vue'),
    meta: { requiresAuth: true, requiresAdmin: true, layout: APP_LAYOUT_ADMIN },
    children: [
      {
        path: '',
        redirect: { name: 'system-admin-boards' }
      },
      {
        path: 'boards',
        name: 'system-admin-boards',
        component: () => import('./views/SystemBoardsView.vue')
      },
      {
        path: 'boards/:boardId(\\d+)/members',
        name: 'system-admin-board-members',
        component: () => import('./views/SystemBoardMembersView.vue')
      },
      {
        path: 'users',
        name: 'users',
        component: () => import('./views/UsersManagerView.vue')
      },
      {
        path: 'machine-access',
        name: 'machine-access',
        component: () => import('./views/MachineAccessView.vue')
      },
      {
        path: 'configuration',
        name: 'configuration',
        component: () => import('./views/ConfigurationView.vue')
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
