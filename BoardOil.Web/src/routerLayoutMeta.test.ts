import { beforeAll, describe, expect, it, vi } from 'vitest';
import type { RouteRecordRaw } from 'vue-router';
import {
  APP_LAYOUT_ADMIN,
  APP_LAYOUT_BOARD_ADMIN,
  APP_LAYOUT_BOARD_WITH_CONVEYOR,
  APP_LAYOUT_STANDARD
} from './site/layouts/appLayout';

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
let indexedRoutes: IndexedRoute[] = [];

beforeAll(async () => {
  await import('./router');
  const firstCall = createRouterMock.mock.calls[0];
  routes = firstCall?.[0]?.routes ?? [];
  indexedRoutes = flattenRoutes(routes);
});

describe('router layout meta mapping', () => {
  it('maps standard routes to APP_LAYOUT_STANDARD', () => {
    expect(findByName('login')?.meta?.layout).toBe(APP_LAYOUT_STANDARD);
    expect(findByName('boards')?.meta?.layout).toBe(APP_LAYOUT_STANDARD);
    expect(findByName('licences')?.meta?.layout).toBe(APP_LAYOUT_STANDARD);
  });

  it('maps board-family routes to APP_LAYOUT_BOARD_WITH_CONVEYOR', () => {
    expect(findIndexedByName('board')?.nearestLayout).toBe(APP_LAYOUT_BOARD_WITH_CONVEYOR);
    expect(findByName('board-archived')?.meta?.layout).toBe(APP_LAYOUT_BOARD_WITH_CONVEYOR);
    expect(findIndexedByName('board-card')?.nearestLayout).toBe(APP_LAYOUT_BOARD_WITH_CONVEYOR);
    expect(findIndexedByName('board-card-transfer')?.nearestLayout).toBe(APP_LAYOUT_BOARD_WITH_CONVEYOR);
  });

  it('maps admin roots to APP_LAYOUT_ADMIN', () => {
    expect(findByPath('/user-admin')?.meta?.layout).toBe(APP_LAYOUT_ADMIN);
    expect(findByPath('/boards/:boardId(\\d+)/admin')?.meta?.layout).toBe(APP_LAYOUT_BOARD_ADMIN);
    expect(findByPath('/admin/system')?.meta?.layout).toBe(APP_LAYOUT_ADMIN);
  });

  it('keeps owner and system OAuth management under their respective admin layouts', () => {
    expect(findIndexedByName('user-admin-oauth-connections')?.nearestLayout).toBe(APP_LAYOUT_ADMIN);
    expect(findIndexedByName('system-admin-oauth-connections')?.nearestLayout).toBe(APP_LAYOUT_ADMIN);
    expect(findIndexedByName('system-oauth-logs')?.nearestLayout).toBe(APP_LAYOUT_ADMIN);
  });

  it('nests user authentication methods beneath the Authentication page', () => {
    expect(findIndexedByName('user-admin-oauth-connections')?.fullPath)
      .toBe('/user-admin/authentication/oauth');
    expect(findIndexedByName('user-admin-access-tokens')?.fullPath)
      .toBe('/user-admin/authentication/access-tokens');
    expect(findIndexedByName('user-admin-mcp-help')?.fullPath)
      .toBe('/user-admin/authentication/mcp-help');

    const authenticationIndex = indexedRoutes.find(
      indexedRoute => indexedRoute.fullPath === '/user-admin/authentication'
        && indexedRoute.route.path === ''
    );
    expect(authenticationIndex?.route.redirect).toEqual({ name: 'user-admin-oauth-connections' });
  });

  it('maps board context requirement to board-scoped route roots', () => {
    expect(findIndexedByName('board')?.requiresBoardContext).toBe(true);
    expect(findByName('board-archived')?.meta?.requiresBoardContext).toBe(true);
    expect(findIndexedByName('board-card')?.requiresBoardContext).toBe(true);
    expect(findIndexedByName('board-card-transfer')?.requiresBoardContext).toBe(true);
    expect(findByPath('/boards/:boardId(\\d+)/admin')?.meta?.requiresBoardContext).toBe(true);
    expect(findByPath('/user-admin')?.meta?.requiresBoardContext).toBeUndefined();
    expect(findByPath('/admin/system')?.meta?.requiresBoardContext).toBeUndefined();
  });

  it('keeps board deep-link dialog routes in named views', () => {
    const dialogRouteNames = [
      'columns-column',
      'tags-new',
      'tags-tag',
      'slicks-new',
      'slicks-slick',
      'card-types-new',
      'card-types-card-type'
    ];

    for (const routeName of dialogRouteNames) {
      const indexedRoute = findIndexedByName(routeName);
      expect(indexedRoute).toBeDefined();
      expect(indexedRoute?.route.components?.default).toBeDefined();
      expect(indexedRoute?.route.components?.dialog).toBeDefined();
    }
  });

  it('nests the card dialog beneath the persistent board workspace', () => {
    const workspaceRoute = findByPath('/boards/:boardId(\\d+)');
    const boardRoute = findIndexedByName('board');
    const cardRoute = findIndexedByName('board-card');
    const transferRoute = findIndexedByName('board-card-transfer');

    expect(workspaceRoute?.component).toBeDefined();
    expect(boardRoute?.fullPath).toBe('/boards/:boardId(\\d+)');
    expect(cardRoute?.fullPath).toBe('/boards/:boardId(\\d+)/card/:cardId(\\d+)');
    expect(cardRoute?.route.components?.default).toBeUndefined();
    expect(cardRoute?.route.components?.dialog).toBeDefined();
    expect(transferRoute?.fullPath).toBe('/boards/:boardId(\\d+)/card/:cardId(\\d+)/transfer');
    expect(transferRoute?.route.components?.default).toBeUndefined();
    expect(transferRoute?.route.components?.dialog).toBeDefined();
  });

  it('keeps system error-log details in the system-admin named dialog view', () => {
    const indexedRoute = findIndexedByName('system-error-log-details');

    expect(indexedRoute?.nearestLayout).toBe(APP_LAYOUT_ADMIN);
    expect(indexedRoute?.route.components?.default).toBeDefined();
    expect(indexedRoute?.route.components?.dialog).toBeDefined();
    expect(indexedRoute?.fullPath).toBe('/admin/system/error-logs/:errorLogId(\\d+)');
  });

  it('keeps board deep-link routes constrained to numeric board ids', () => {
    const boardRouteNames = [
      'board',
      'board-archived',
      'board-card',
      'board-card-transfer',
      'board-details',
      'columns',
      'columns-column',
      'tags',
      'tags-new',
      'tags-tag',
      'slicks',
      'slicks-new',
      'slicks-slick',
      'card-types',
      'card-types-new',
      'card-types-card-type',
      'board-members',
      'board-delete'
    ];

    for (const routeName of boardRouteNames) {
      const indexedRoute = findIndexedByName(routeName);
      expect(indexedRoute).toBeDefined();
      expect(indexedRoute?.fullPath.includes(':boardId(\\d+)')).toBe(true);
    }
  });

  it('keeps board admin children under board-admin layout and context requirement', () => {
    const boardAdminChildren = [
      'board-details',
      'columns',
      'columns-column',
      'tags',
      'tags-new',
      'tags-tag',
      'slicks',
      'slicks-new',
      'slicks-slick',
      'card-types',
      'card-types-new',
      'card-types-card-type',
      'board-members',
      'board-delete'
    ];

    for (const routeName of boardAdminChildren) {
      const indexedRoute = findIndexedByName(routeName);
      expect(indexedRoute).toBeDefined();
      expect(indexedRoute?.nearestLayout).toBe(APP_LAYOUT_BOARD_ADMIN);
      expect(indexedRoute?.requiresBoardContext).toBe(true);
    }
  });
});

function findByName(name: string) {
  return routes.find(route => route.name === name);
}

function findByPath(path: string) {
  return routes.find(route => route.path === path);
}

function findIndexedByName(name: string) {
  return indexedRoutes.find(indexedRoute => indexedRoute.route.name === name);
}

function flattenRoutes(
  sourceRoutes: RouteRecordRaw[],
  ancestors: RouteRecordRaw[] = [],
  inheritedPath = ''
): IndexedRoute[] {
  const indexed: IndexedRoute[] = [];

  for (const route of sourceRoutes) {
    const fullPath = joinRoutePath(inheritedPath, route.path);
    const nextAncestors = [...ancestors, route];
    const nearestLayout = findNearestLayout(nextAncestors);
    const requiresBoardContext = nextAncestors.some(
      ancestor => ancestor.meta?.requiresBoardContext === true
    );

    indexed.push({
      route,
      fullPath,
      nearestLayout,
      requiresBoardContext
    });

    if (route.children) {
      indexed.push(...flattenRoutes(route.children, nextAncestors, fullPath));
    }
  }

  return indexed;
}

function joinRoutePath(parentPath: string, path: string) {
  if (path.startsWith('/')) {
    return path;
  }

  if (!parentPath) {
    return path;
  }

  if (!path) {
    return parentPath;
  }

  return `${parentPath}/${path}`.replace(/\/{2,}/g, '/');
}

function findNearestLayout(ancestors: RouteRecordRaw[]) {
  for (let index = ancestors.length - 1; index >= 0; index -= 1) {
    const layout = ancestors[index]?.meta?.layout;
    if (typeof layout === 'string') {
      return layout;
    }
  }

  return null;
}

type IndexedRoute = {
  route: RouteRecordRaw;
  fullPath: string;
  nearestLayout: string | null;
  requiresBoardContext: boolean;
};
