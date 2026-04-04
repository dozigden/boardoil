import { describe, expect, it } from 'vitest';
import { isClickOutsideElement } from './useClickOutside';

describe('isClickOutsideElement', () => {
  it('returns false when the composed path contains the element', () => {
    const root = { contains: () => false } as unknown as HTMLElement;
    const inner = {};
    const event = {
      target: inner,
      composedPath: () => [inner, root]
    } as any;

    expect(isClickOutsideElement(event, root)).toBe(false);
  });

  it('returns false when the target is contained by the element', () => {
    const target = {};
    const root = {
      contains(candidate: Node) {
        return candidate === target;
      }
    } as unknown as HTMLElement;
    const event = {
      target
    } as any;

    expect(isClickOutsideElement(event, root)).toBe(false);
  });

  it('returns true when the click lands outside the element', () => {
    const root = {
      contains: () => false
    } as unknown as HTMLElement;
    const event = {
      target: {}
    } as any;

    expect(isClickOutsideElement(event, root)).toBe(true);
  });
});
