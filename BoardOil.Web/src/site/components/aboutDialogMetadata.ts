import type { BuildInfo } from '../../shared/api/versionApi';

export type AboutSummaryItem = {
  label: string;
  buildInfo: BuildInfo;
};

export function areBuildMetadataInSync(frontendBuildInfo: BuildInfo, backendBuildInfo: BuildInfo | null) {
  if (!backendBuildInfo) {
    return null;
  }

  return normaliseVersion(frontendBuildInfo.version) === normaliseVersion(backendBuildInfo.version)
    && normalise(frontendBuildInfo.channel) === normalise(backendBuildInfo.channel)
    && normalise(frontendBuildInfo.build) === normalise(backendBuildInfo.build)
    && normalise(frontendBuildInfo.commit) === normalise(backendBuildInfo.commit);
}

export function getAboutSummaryItems(
  frontendBuildInfo: BuildInfo,
  backendBuildInfo: BuildInfo | null,
  buildMetadataInSync: boolean | null
): AboutSummaryItem[] {
  if (buildMetadataInSync === false && backendBuildInfo) {
    return [
      { label: 'Frontend', buildInfo: frontendBuildInfo },
      { label: 'Backend', buildInfo: backendBuildInfo }
    ];
  }

  if (backendBuildInfo) {
    return [{ label: 'Runtime', buildInfo: backendBuildInfo }];
  }

  return [{ label: 'Frontend', buildInfo: frontendBuildInfo }];
}

export function formatBuildInfo(buildInfo: BuildInfo) {
  return `${buildInfo.version} (${buildInfo.channel}/${buildInfo.build}) ${formatCommit(buildInfo.commit)}`;
}

export function formatCommit(commit: string) {
  const trimmed = commit.trim();
  if (!trimmed || trimmed === 'unknown') {
    return 'unknown';
  }

  return trimmed.length > 12 ? trimmed.slice(0, 12) : trimmed;
}

function normalise(value: string) {
  return value.trim().toLowerCase();
}

function normaliseVersion(value: string) {
  return normalise(value.split('+', 1)[0] ?? value);
}
