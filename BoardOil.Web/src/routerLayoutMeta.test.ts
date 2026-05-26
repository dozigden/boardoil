import { beforeAll, describe, expect, it, vi } from 'vitest';
import type { RouteRecordRaw } from 'vue-router';
import { APP_LAYOUT_ADMIN, APP_LAYOUT_BOARD_WITH_CONVEYOR, APP_LAYOUT_STANDARD } from './site/layouts/appLayout';

const createRouterMock = vi.fn(
  (options: { routes: RouteRecordRaw[] }) => ({
    getRoutes: () => options.routes,
    beforeEach: vi.fn()
  })
);

vi.mock('vue-router', () => ({
  createWebHistory: vi.fn(() => ({})),
  createRouter: createRouterMock
}));

vi.mock('./site/auth/navigationGuard', () => ({
  resolveAuthNavigation: vi.fn(async () => true)
}));

vi.mock('./shared/stores/authStore', () => ({
  useAuthStore: vi.fn(() => ({}))
}));

let routes: RouteRecordRaw[] = [];

beforeAll(async () => {
  await import('./router');
  const firstCall = createRouterMock.mock.calls[0];
  routes = firstCall?.[0]?.routes ?? [];
});

describe('router layout meta mapping', () => {
  it('maps standard routes to APP_LAYOUT_STANDARD', () => {
    expect(findByName('login')?.meta?.layout).toBe(APP_LAYOUT_STANDARD);
    expect(findByName('boards')?.meta?.layout).toBe(APP_LAYOUT_STANDARD);
    expect(findByName('licences')?.meta?.layout).toBe(APP_LAYOUT_STANDARD);
  });

  it('maps board-family routes to APP_LAYOUT_BOARD_WITH_CONVEYOR', () => {
    expect(findByName('board')?.meta?.layout).toBe(APP_LAYOUT_BOARD_WITH_CONVEYOR);
    expect(findByName('board-archived')?.meta?.layout).toBe(APP_LAYOUT_BOARD_WITH_CONVEYOR);
    expect(findByName('board-card')?.meta?.layout).toBe(APP_LAYOUT_BOARD_WITH_CONVEYOR);
  });

  it('maps admin roots to APP_LAYOUT_ADMIN', () => {
    expect(findByPath('/user-admin')?.meta?.layout).toBe(APP_LAYOUT_ADMIN);
    expect(findByPath('/boards/:boardId(\\d+)/admin')?.meta?.layout).toBe(APP_LAYOUT_ADMIN);
    expect(findByPath('/admin/system')?.meta?.layout).toBe(APP_LAYOUT_ADMIN);
  });
});

function findByName(name: string) {
  return routes.find(route => route.name === name);
}

function findByPath(path: string) {
  return routes.find(route => route.path === path);
}
