import { describe, expect, it } from 'vitest';
import type { BuildInfo } from '../../shared/api/versionApi';
import { areBuildMetadataInSync, formatBuildInfo, getAboutSummaryItems } from './aboutDialogMetadata';

function createBuildInfo(overrides?: Partial<BuildInfo>): BuildInfo {
  return {
    version: '0.1.0',
    channel: 'dev',
    build: 'local',
    commit: '0123456789abcdef',
    ...overrides
  };
}

describe('aboutDialogMetadata', () => {
  it('returns null sync state when backend metadata is unavailable', () => {
    const frontend = createBuildInfo();

    expect(areBuildMetadataInSync(frontend, null)).toBeNull();
  });

  it('treats semantic-version build metadata suffixes as in sync', () => {
    const frontend = createBuildInfo({ version: '0.1.0' });
    const backend = createBuildInfo({ version: '0.1.0+sha.abcdef' });

    expect(areBuildMetadataInSync(frontend, backend)).toBe(true);
  });

  it('detects mismatch when a build metadata field differs', () => {
    const frontend = createBuildInfo({ commit: '1111111111111111' });
    const backend = createBuildInfo({ commit: '2222222222222222' });

    expect(areBuildMetadataInSync(frontend, backend)).toBe(false);
  });

  it('shows frontend-only summary when backend metadata is unavailable', () => {
    const frontend = createBuildInfo();

    expect(getAboutSummaryItems(frontend, null, null)).toEqual([
      { label: 'Frontend', buildInfo: frontend }
    ]);
  });

  it('shows runtime summary when frontend and backend metadata are in sync', () => {
    const frontend = createBuildInfo();
    const backend = createBuildInfo();

    expect(getAboutSummaryItems(frontend, backend, true)).toEqual([
      { label: 'Runtime', buildInfo: backend }
    ]);
  });

  it('shows separate frontend/backend summaries when metadata differs', () => {
    const frontend = createBuildInfo({ commit: '1111111111111111' });
    const backend = createBuildInfo({ commit: '2222222222222222' });

    expect(getAboutSummaryItems(frontend, backend, false)).toEqual([
      { label: 'Frontend', buildInfo: frontend },
      { label: 'Backend', buildInfo: backend }
    ]);
  });

  it('formats build info with commit shortened to 12 characters', () => {
    const buildInfo = createBuildInfo({ commit: 'abcdef0123456789' });

    expect(formatBuildInfo(buildInfo)).toBe('0.1.0 (dev/local) abcdef012345');
  });
});
