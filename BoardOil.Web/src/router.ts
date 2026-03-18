import { createRouter, createWebHistory } from 'vue-router';
import type { RouteRecordRaw } from 'vue-router';
import { resolveAuthNavigation } from './auth/navigationGuard';
import { useAuthStore } from './stores/authStore';

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'login',
    component: () => import('./views/LoginView.vue'),
    meta: { requiresAuth: false }
  },
  {
    path: '/setup-initial-admin',
    name: 'setup-initial-admin',
    component: () => import('./views/SetupInitialAdminView.vue'),
    meta: { requiresAuth: false }
  },
  {
    path: '/',
    name: 'board',
    component: () => import('./views/BoardView.vue'),
    meta: { requiresAuth: true }
  },
  {
    path: '/card/:cardId(\\d+)',
    name: 'board-card',
    components: {
      default: () => import('./views/BoardView.vue'),
      dialog: () => import('./components/CardEditorDialog.vue')
    },
    meta: { requiresAuth: true }
  },
  {
    path: '/columns',
    name: 'columns',
    component: () => import('./views/ColumnsManagerView.vue'),
    meta: { requiresAuth: true, requiresAdmin: true }
  },
  {
    path: '/columns/:columnId(\\d+)',
    name: 'columns-column',
    components: {
      default: () => import('./views/ColumnsManagerView.vue'),
      dialog: () => import('./components/ColumnEditorDialog.vue')
    },
    meta: { requiresAuth: true, requiresAdmin: true }
  },
  {
    path: '/users',
    name: 'users',
    component: () => import('./views/UsersManagerView.vue'),
    meta: { requiresAuth: true, requiresAdmin: true }
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
