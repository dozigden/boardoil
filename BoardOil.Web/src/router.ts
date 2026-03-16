import { createRouter, createWebHistory } from 'vue-router';
import type { RouteRecordRaw } from 'vue-router';
import { useAuthStore } from './stores/authStore';

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'login',
    component: () => import('./views/LoginView.vue'),
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
  if (!authStore.initialized) {
    await authStore.initialize();
  }

  if (to.name === 'login' && authStore.isAuthenticated) {
    return { name: 'board' };
  }

  const requiresAuth = to.matched.some(record => record.meta.requiresAuth !== false);
  if (requiresAuth && !authStore.isAuthenticated) {
    return { name: 'login' };
  }

  const requiresAdmin = to.matched.some(record => record.meta.requiresAdmin === true);
  if (requiresAdmin && !authStore.isAdmin) {
    return { name: 'board' };
  }

  return true;
});
