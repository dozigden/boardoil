import { describe, expect, it } from 'vitest';
import { buildLoginRedirectQuery, getSafeRedirectTarget } from './redirectTarget';

describe('redirectTarget', () => {
  it('builds redirect query for safe internal paths', () => {
    expect(buildLoginRedirectQuery('/boards/5/card/20?x=1')).toEqual({ redirect: '/boards/5/card/20?x=1' });
  });

  it('does not build redirect query for invalid paths', () => {
    expect(buildLoginRedirectQuery(undefined)).toBeUndefined();
    expect(buildLoginRedirectQuery('https://evil.test')).toBeUndefined();
    expect(buildLoginRedirectQuery('//evil.test')).toBeUndefined();
  });

  it('returns safe redirect target when provided', () => {
    expect(getSafeRedirectTarget('/boards/5')).toBe('/boards/5');
  });

  it('rejects external, malformed, and auth-loop redirect targets', () => {
    expect(getSafeRedirectTarget('https://evil.test')).toBeNull();
    expect(getSafeRedirectTarget('//evil.test')).toBeNull();
    expect(getSafeRedirectTarget('boards/5')).toBeNull();
    expect(getSafeRedirectTarget('/login')).toBeNull();
    expect(getSafeRedirectTarget('/setup-initial-admin')).toBeNull();
    expect(getSafeRedirectTarget('/unauthorized')).toBeNull();
  });
});
