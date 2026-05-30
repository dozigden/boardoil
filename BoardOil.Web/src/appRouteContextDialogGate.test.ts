import { describe, expect, it } from 'vitest';
import appSfc from './App.vue?raw';

function countOccurrences(content: string, fragment: string) {
  return content.split(fragment).length - 1;
}

describe('App board-context dialog gating', () => {
  it('keeps root dialog route gated by board-route context readiness', () => {
    expect(countOccurrences(appSfc, '<RouterView v-if="!hideRootDialogView" name="dialog" />')).toBe(1);
    expect(appSfc.includes('const hideRootDialogView = computed(() => !hasBoardRouteContext.value);')).toBe(true);
    expect(appSfc.includes('const hasBoardRouteContext = computed(() => {')).toBe(true);
    expect(appSfc.includes('route.matched.some(matchedRoute => matchedRoute.meta.requiresBoardContext === true)')).toBe(true);
    expect(appSfc.includes('currentBoardId.value === routeBoardId.value')).toBe(true);
    expect(appSfc.includes('board.value?.id === routeBoardId.value')).toBe(true);
  });
});
