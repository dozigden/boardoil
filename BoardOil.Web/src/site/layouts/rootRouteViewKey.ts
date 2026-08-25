import type { RouteLocationNormalizedLoaded } from 'vue-router';

type RootRouteViewLocation = Pick<RouteLocationNormalizedLoaded, 'matched' | 'name' | 'params'>;

export function getRootRouteViewKey(route: RootRouteViewLocation) {
  const rootRecord = route.matched[0];
  if (!rootRecord) {
    const routeName = typeof route.name === 'string' ? route.name : 'route';
    return `${routeName}:${JSON.stringify(route.params ?? {})}`;
  }

  const rootParams = getRouteRecordParams(rootRecord.path, route.params);
  const rootIdentity = rootRecord.name ?? rootRecord.path;
  return `${String(rootIdentity)}:${JSON.stringify(rootParams)}`;
}

function getRouteRecordParams(
  path: string,
  params: RouteLocationNormalizedLoaded['params']
) {
  const rootParams: Record<string, unknown> = {};

  for (const match of path.matchAll(/:([A-Za-z0-9_]+)/g)) {
    const paramName = match[1];
    if (paramName) {
      rootParams[paramName] = params[paramName];
    }
  }

  return rootParams;
}
