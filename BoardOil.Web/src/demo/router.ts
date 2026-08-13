import { createRouter, createWebHashHistory } from 'vue-router';

const routes = [
  {
    path: '/',
    redirect: { name: 'board', params: { boardId: '1' } }
  },
  {
    path: '/boards/:boardId(\\d+)',
    name: 'board',
    component: () => import('../board/views/BoardView.vue')
  },
  {
    path: '/boards/:boardId(\\d+)/archived',
    name: 'board-archived',
    component: () => import('../board/views/ArchivedCardsView.vue')
  },
  {
    path: '/boards/:boardId(\\d+)/card/:cardId(\\d+)',
    name: 'board-card',
    components: {
      default: () => import('../board/views/BoardView.vue'),
      dialog: () => import('../board/components/CardEditorDialog.vue')
    }
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
