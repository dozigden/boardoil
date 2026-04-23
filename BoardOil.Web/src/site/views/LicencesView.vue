<template>
  <section class="licences-view">
    <header class="licences-header">
      <h2>Third-Party Licences</h2>
      <p>Licence information for npm packages, NuGet packages, and bundled third-party assets.</p>
    </header>

    <p v-if="loading" class="licences-state">Loading licences...</p>
    <p v-else-if="manifestError" class="error">{{ manifestError }}</p>

    <template v-else>
      <section v-if="licenceEntries.length > 0" class="licences-list">
        <details
          v-for="entry in licenceEntries"
          :key="`${entry.ecosystem}:${entry.packageName}:${entry.version}`"
          class="panel"
        >
          <summary class="licence-summary">
            <span class="badge badge--ecosystem">{{ getEcosystemLabel(entry.ecosystem) }}</span>
            <span class="licence-package">{{ entry.packageName }}</span>
            <span class="badge">v{{ entry.version }}</span>
            <span class="badge">{{ entry.declaredLicence }}</span>
          </summary>
          <p v-if="entry.errorMessage" class="error">{{ entry.errorMessage }}</p>
          <pre v-else class="licence-text">{{ entry.text }}</pre>
        </details>
      </section>
      <p v-else class="licences-state">No licence entries found in manifest.</p>

      <section v-if="unresolvedPackages.length > 0" class="panel panel-stack panel-stack--tight licences-unresolved">
        <h3>Some Licence Information Is Unavailable</h3>
        <p class="licences-unresolved-hint">
          Some third-party licence information could not be loaded right now.
        </p>
        <ul class="licences-unresolved-list">
          <li
            v-for="entry in unresolvedPackages"
            :key="`${entry.ecosystem ?? 'unknown'}:${entry.packageName}:${entry.version ?? 'unknown'}`"
            class="licences-unresolved-item"
          >
            <p class="licences-unresolved-title">
              <span class="badge badge--ecosystem">{{ getEcosystemLabel(entry.ecosystem) }}</span>
              <strong>{{ entry.packageName }}</strong>
              <span class="badge">v{{ entry.version ?? 'unknown' }}</span>
              <span class="badge">{{ entry.declaredLicence ?? 'unknown' }}</span>
            </p>
            <p class="licences-unresolved-reason">{{ entry.reason }}</p>
            <p v-if="entry.expectedSourceFile" class="licences-unresolved-resolution">
              {{ entry.resolutionHint ?? 'Expected source file:' }}
              <code>{{ entry.expectedSourceFile }}</code>
            </p>
          </li>
        </ul>
      </section>
    </template>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';

type CopiedLicence = {
  ecosystem?: 'npm' | 'nuget' | 'asset';
  packageName: string;
  version: string;
  declaredLicence: string;
  sourceType:
    | 'package'
    | 'manual'
    | 'asset'
    | 'nuget-package-file'
    | 'nuget-license-expression'
    | 'nuget-license-url';
  sourceFile: string;
  outputFile: string;
};

type UnresolvedPackage = {
  ecosystem?: 'npm' | 'nuget' | 'asset';
  packageName: string;
  version: string;
  declaredLicence: string;
  reason: string;
  expectedSourceFile?: string;
  resolutionHint?: string;
};

type LicenceManifest = {
  generatedAtUtc?: string;
  packageSource: string;
  copiedLicences: CopiedLicence[];
  unresolvedPackages: UnresolvedPackage[];
};

type LicenceEntry = CopiedLicence & {
  ecosystem: 'npm' | 'nuget' | 'asset';
  text: string;
  errorMessage: string | null;
};

const loading = ref(true);
const manifestError = ref<string | null>(null);
const manifest = ref<LicenceManifest | null>(null);
const licenceEntries = ref<LicenceEntry[]>([]);

const unresolvedPackages = computed(() => manifest.value?.unresolvedPackages ?? []);

onMounted(async () => {
  await loadLicences();
});

function normaliseEcosystem(entry: Pick<CopiedLicence, 'ecosystem' | 'sourceType' | 'outputFile'>):
  | 'npm'
  | 'nuget'
  | 'asset' {
  if (entry.ecosystem === 'npm' || entry.ecosystem === 'nuget' || entry.ecosystem === 'asset') {
    return entry.ecosystem;
  }

  if (entry.sourceType === 'asset') {
    return 'asset';
  }

  if (entry.outputFile.includes('nuget-') || entry.sourceType.startsWith('nuget-')) {
    return 'nuget';
  }

  return 'npm';
}

function getEcosystemLabel(ecosystem: string | undefined) {
  if (ecosystem === 'nuget') {
    return 'NuGet';
  }

  if (ecosystem === 'asset') {
    return 'Asset';
  }

  return 'NPM';
}

async function loadLicences() {
  loading.value = true;
  manifestError.value = null;

  try {
    const manifestResponse = await fetch('/third-party-licenses/manifest.json', { cache: 'no-store' });
    if (!manifestResponse.ok) {
      throw new Error(`Failed to load manifest (HTTP ${manifestResponse.status}).`);
    }

    const manifestPayload = await manifestResponse.text();
    let parsedManifest: LicenceManifest;
    try {
      parsedManifest = JSON.parse(manifestPayload) as LicenceManifest;
    } catch {
      throw new Error(
        'Third-party licence manifest is missing or invalid. Run `npm run sync:third-party-licences`.'
      );
    }
    manifest.value = parsedManifest;

    const entries = await Promise.all(
      parsedManifest.copiedLicences.map(async copiedLicence => {
        const ecosystem = normaliseEcosystem(copiedLicence);
        const fileName = copiedLicence.outputFile.split('/').pop();
        if (!fileName) {
          return {
            ...copiedLicence,
            ecosystem,
            text: '',
            errorMessage: 'Manifest entry is missing an output file name.'
          } satisfies LicenceEntry;
        }

        try {
          const textResponse = await fetch(`/third-party-licenses/${encodeURIComponent(fileName)}`, { cache: 'no-store' });
          if (!textResponse.ok) {
            throw new Error(`HTTP ${textResponse.status}`);
          }

          return {
            ...copiedLicence,
            ecosystem,
            text: await textResponse.text(),
            errorMessage: null
          } satisfies LicenceEntry;
        } catch (error) {
          const suffix = error instanceof Error ? ` ${error.message}` : '';
          return {
            ...copiedLicence,
            ecosystem,
            text: '',
            errorMessage: `Could not load licence file.${suffix}`
          } satisfies LicenceEntry;
        }
      })
    );

    entries.sort((left, right) => {
      const byEcosystem = left.ecosystem.localeCompare(right.ecosystem);
      if (byEcosystem !== 0) {
        return byEcosystem;
      }

      const byPackageName = left.packageName.localeCompare(right.packageName);
      if (byPackageName !== 0) {
        return byPackageName;
      }

      return left.version.localeCompare(right.version);
    });

    licenceEntries.value = entries;
  } catch (error) {
    manifestError.value = error instanceof Error ? error.message : 'Failed to load third-party licences.';
  } finally {
    loading.value = false;
  }
}
</script>

<style scoped>
.licences-view {
  margin-top: 1rem;
  margin-left: 2rem;
  display: grid;
  gap: 0.9rem;
  max-width: 960px;
}

.licences-header h2 {
  margin: 0;
}

.licences-header p {
  margin: 0.2rem 0 0;
  color: var(--bo-ink-muted);
}

.licences-state {
  margin: 0;
  color: var(--bo-ink-muted);
}

.licences-list {
  display: grid;
  gap: 0.55rem;
}

.licence-summary {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  flex-wrap: wrap;
  cursor: pointer;
  list-style: none;
}

.licence-summary::-webkit-details-marker {
  display: none;
}

.licence-package {
  font-weight: 700;
  color: var(--bo-ink-strong);
}

.badge--ecosystem {
  letter-spacing: 0.03em;
}

.licence-text {
  margin: 0.75rem 0 0;
  padding: 0.75rem;
  border: 1px solid var(--bo-border-soft);
  border-radius: 10px;
  background: var(--bo-surface-base);
  color: var(--bo-ink-strong);
  white-space: pre-wrap;
  overflow-wrap: anywhere;
  max-height: 24rem;
  overflow: auto;
  font-family: "Cascadia Mono", "Consolas", "Liberation Mono", monospace;
  font-size: 0.83rem;
  line-height: 1.35;
}

.licences-unresolved h3,
.licences-unresolved-hint {
  margin: 0;
}

.licences-unresolved-hint {
  color: var(--bo-ink-muted);
}

.licences-unresolved-list {
  margin: 0;
  padding: 0;
  list-style: none;
  display: grid;
  gap: 0.45rem;
}

.licences-unresolved-item {
  border: 1px solid var(--bo-border-soft);
  border-radius: 10px;
  padding: 0.65rem 0.75rem;
  background: var(--bo-surface-base);
}

.licences-unresolved-title,
.licences-unresolved-reason,
.licences-unresolved-resolution {
  margin: 0;
}

.licences-unresolved-title {
  display: flex;
  gap: 0.4rem;
  flex-wrap: wrap;
  align-items: center;
}

.licences-unresolved-reason,
.licences-unresolved-resolution {
  margin-top: 0.3rem;
  color: var(--bo-ink-muted);
}
</style>
