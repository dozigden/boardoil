import { createRouter, createWebHistory } from 'vue-router';
import type { RouteRecordRaw } from 'vue-router';

const routes: RouteRecordRaw[] = [
  {
    path: '/',
    name: 'board',
    component: () => import('./views/BoardView.vue')
  },
  {
    path: '/card/:cardId(\\d+)',
    name: 'board-card',
    components: {
      default: () => import('./views/BoardView.vue'),
      dialog: () => import('./components/CardEditorDialog.vue')
    }
  },
  {
    path: '/columns',
    name: 'columns',
    component: () => import('./views/ColumnsManagerView.vue')
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
