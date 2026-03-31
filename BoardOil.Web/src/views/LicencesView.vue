<template>
  <section class="licences-view">
    <header class="licences-header">
      <h2>Third-Party Licences</h2>
      <p>Licence information for third-party software included with this application.</p>
    </header>

    <p v-if="loading" class="licences-state">Loading licences...</p>
    <p v-else-if="manifestError" class="error">{{ manifestError }}</p>

    <template v-else>
      <section v-if="licenceEntries.length > 0" class="licences-list">
        <details v-for="entry in licenceEntries" :key="entry.packageName" class="panel">
          <summary class="licence-summary">
            <span class="licence-package">{{ entry.packageName }}</span>
            <span class="card-id">v{{ entry.version }}</span>
            <span class="card-id">{{ entry.declaredLicence }}</span>
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
      </section>
    </template>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';

type CopiedLicence = {
  packageName: string;
  version: string;
  declaredLicence: string;
  sourceType: 'package' | 'manual' | 'asset';
  sourceFile: string;
  outputFile: string;
};

type UnresolvedPackage = {
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

async function loadLicences() {
  loading.value = true;
  manifestError.value = null;

  try {
    const manifestResponse = await fetch('/third-party-licenses/manifest.json', { cache: 'no-store' });
    if (!manifestResponse.ok) {
      throw new Error(`Failed to load manifest (HTTP ${manifestResponse.status}).`);
    }

    const parsedManifest = (await manifestResponse.json()) as LicenceManifest;
    manifest.value = parsedManifest;

    const entries = await Promise.all(
      parsedManifest.copiedLicences.map(async copiedLicence => {
        const fileName = copiedLicence.outputFile.split('/').pop();
        if (!fileName) {
          return {
            ...copiedLicence,
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
            text: await textResponse.text(),
            errorMessage: null
          } satisfies LicenceEntry;
        } catch (error) {
          const suffix = error instanceof Error ? ` ${error.message}` : '';
          return {
            ...copiedLicence,
            text: '',
            errorMessage: `Could not load licence file.${suffix}`
          } satisfies LicenceEntry;
        }
      })
    );

    licenceEntries.value = entries;
  } catch (error) {
    manifestError.value = error instanceof Error ? error.message : 'Failed to load third-party licences.';
  } finally {
    loading.value = false;
  }
}
</script>
