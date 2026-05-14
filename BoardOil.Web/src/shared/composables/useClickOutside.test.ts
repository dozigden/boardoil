import { describe, expect, it } from 'vitest';
import { isClickOutsideElement, resolveClickOutsideElement } from './useClickOutside';

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

describe('resolveClickOutsideElement', () => {
  it('returns an empty array for nullish values', () => {
    expect(resolveClickOutsideElement(null)).toEqual([]);
    expect(resolveClickOutsideElement(undefined)).toEqual([]);
  });

  it('returns an array containing the element when value is a single element', () => {
    const element = {} as HTMLElement;
    expect(resolveClickOutsideElement(element)).toEqual([element]);
  });

  it('returns all elements when value is an array ref', () => {
    const first = {} as HTMLElement;
    const second = {} as HTMLElement;
    expect(resolveClickOutsideElement([first, second])).toEqual([first, second]);
  });
});
