import { createRouter, createWebHashHistory } from 'vue-router';
import type { RouteRecordRaw } from 'vue-router';

const routes: RouteRecordRaw[] = [
  {
    path: '/',
    redirect: { name: 'board', params: { boardId: '1' } }
  },
  {
    path: '/boards/:boardId(\\d+)',
    component: () => import('../board/views/BoardWorkspaceView.vue'),
    children: [
      {
        path: '',
        name: 'board',
        components: {}
      },
      {
        path: 'card/:cardId(\\d+)',
        name: 'board-card',
        components: {
          dialog: () => import('../board/components/CardEditorDialog.vue')
        }
      }
    ]
  },
  {
    path: '/boards/:boardId(\\d+)/archived',
    name: 'board-archived',
    component: () => import('../board/views/ArchivedCardsView.vue')
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: { name: 'board', params: { boardId: '1' } }
  }
];

export const demoRouter = createRouter({
  history: createWebHashHistory(),
  routes
});
