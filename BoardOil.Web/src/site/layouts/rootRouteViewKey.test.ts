import { describe, expect, it } from 'vitest';
import { getRootRouteViewKey } from './rootRouteViewKey';

describe('getRootRouteViewKey', () => {
  it('keeps a nested route shell stable when only child params change', () => {
    const boardKey = getRootRouteViewKey(createRoute(
      'board',
      { boardId: '1' },
      ['/boards/:boardId(\\d+)']
    ));
    const cardKey = getRootRouteViewKey(createRoute(
      'board-card',
      { boardId: '1', cardId: '792' },
      ['/boards/:boardId(\\d+)', 'card/:cardId(\\d+)']
    ));

    expect(cardKey).toBe(boardKey);
  });

  it('remounts the route shell when one of its own params changes', () => {
    const firstBoardKey = getRootRouteViewKey(createRoute(
      'board',
      { boardId: '1' },
      ['/boards/:boardId(\\d+)']
    ));
    const secondBoardKey = getRootRouteViewKey(createRoute(
      'board',
      { boardId: '2' },
      ['/boards/:boardId(\\d+)']
    ));

    expect(secondBoardKey).not.toBe(firstBoardKey);
  });

  it('uses a different key for a different root route record', () => {
    const boardKey = getRootRouteViewKey(createRoute(
      'board',
      { boardId: '1' },
      ['/boards/:boardId(\\d+)']
    ));
    const archiveKey = getRootRouteViewKey(createRoute(
      'board-archived',
      { boardId: '1' },
      ['/boards/:boardId(\\d+)/archived']
    ));

    expect(archiveKey).not.toBe(boardKey);
  });
});

function createRoute(
  name: string,
  params: Record<string, string>,
  matchedPaths: string[]
) {
  return {
    name,
    params,
    matched: matchedPaths.map(path => ({ path }))
  } as Parameters<typeof getRootRouteViewKey>[0];
}
