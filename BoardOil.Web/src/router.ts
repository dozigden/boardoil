import { createRouter, createWebHistory } from 'vue-router';

const routes = [
  {
    path: '/',
    name: 'board',
    component: () => import('./views/BoardView.vue')
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
