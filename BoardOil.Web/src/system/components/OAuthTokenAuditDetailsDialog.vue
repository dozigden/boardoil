<template>
  <FixedChromeDialog
    class="oauth-token-audit-details-modal"
    :open="audit !== null"
    title="OAuth Log Details"
    close-label="Close OAuth log details"
    size="fill"
    body-mode="managed"
    @close="emit('close')"
  >
    <template #headerActions>
      <button
        type="button"
        class="btn btn--secondary"
        :disabled="audit === null"
        @click="copyDetails"
      >
        Copy details
      </button>
    </template>

    <article v-if="audit" class="oauth-token-audit-report" @copy="onReportCopy">
      <h4>OAuth log #{{ audit.id }}</h4>

      <dl class="oauth-token-audit-report-facts">
        <div>
          <dt>Occurred</dt>
          <dd>{{ formatDate(audit.occurredAtUtc) }}</dd>
        </div>
        <div>
          <dt>Outcome</dt>
          <dd>{{ audit.outcome }}</dd>
        </div>
        <div>
          <dt>Grant type</dt>
          <dd>{{ formatGrantType(audit.grantType) }}</dd>
        </div>
        <div>
          <dt>Requested scopes</dt>
          <dd>{{ formatValue(audit.requestedScopes) }}</dd>
        </div>
        <div>
          <dt>Connection</dt>
          <dd>{{ formatConnection(audit) }}</dd>
        </div>
        <div>
          <dt>Connection ID</dt>
          <dd>{{ formatReference(audit.oauthConnectionId) }}</dd>
        </div>
        <div>
          <dt>Owner</dt>
          <dd>{{ formatOwner(audit) }}</dd>
        </div>
        <div>
          <dt>OAuth client</dt>
          <dd>{{ formatOAuthClient(audit) }}</dd>
        </div>
        <div>
          <dt>Authorization ID</dt>
          <dd>{{ formatValue(audit.authorizationId) }}</dd>
        </div>
        <div>
          <dt>Resource</dt>
          <dd>{{ formatValue(audit.resource) }}</dd>
        </div>
        <div>
          <dt>Trace</dt>
          <dd>{{ formatValue(audit.traceIdentifier) }}</dd>
        </div>
        <div>
          <dt>User agent</dt>
          <dd>{{ formatValue(audit.userAgent) }}</dd>
        </div>
      </dl>

      <section>
        <h5>Token fingerprints</h5>
        <dl class="oauth-token-audit-report-facts">
          <div>
            <dt>Presented token</dt>
            <dd>{{ formatValue(audit.presentedTokenFingerprint) }}</dd>
          </div>
          <div>
            <dt>Issued refresh token</dt>
            <dd>{{ formatValue(audit.issuedRefreshTokenFingerprint) }}</dd>
          </div>
        </dl>
      </section>

      <section>
        <h5>Error</h5>
        <dl class="oauth-token-audit-report-facts">
          <div>
            <dt>Code</dt>
            <dd>{{ formatValue(audit.errorCode) }}</dd>
          </div>
          <div>
            <dt>Description</dt>
            <dd>{{ formatValue(audit.errorDescription) }}</dd>
          </div>
          <div>
            <dt>URI</dt>
            <dd>{{ formatValue(audit.errorUri) }}</dd>
          </div>
        </dl>
      </section>
    </article>

    <template #actions>
      <div class="fixed-chrome-dialog-actions fixed-chrome-dialog-actions--end">
        <button type="button" class="btn btn--secondary" @click="emit('close')">Close</button>
      </div>
    </template>
  </FixedChromeDialog>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import FixedChromeDialog from '../../shared/components/FixedChromeDialog.vue';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { OAuthTokenAudit } from '../../shared/types/oauthTokenAuditTypes';
import { copyTextToClipboard } from '../../shared/utils/clipboard';
import {
  buildOAuthTokenAuditMarkdown,
  formatConnection,
  formatGrantType,
  formatOAuthClient,
  formatOwner,
  formatReference,
  formatValue
} from '../utils/oauthTokenAuditMarkdown';

const props = defineProps<{
  audit: OAuthTokenAudit | null;
}>();
const emit = defineEmits<{
  close: [];
}>();
const feedback = useUiFeedbackStore();
const markdownText = computed(() => {
  if (props.audit === null) {
    return '';
  }

  return buildOAuthTokenAuditMarkdown(props.audit, formatDate(props.audit.occurredAtUtc));
});

async function copyDetails() {
  if (!markdownText.value) {
    return;
  }

  const copied = await copyTextToClipboard(markdownText.value);
  if (copied) {
    feedback.showToast('Copied');
    return;
  }

  feedback.showToast('Could not copy automatically. Select the report and copy it manually.', 'error');
}

function onReportCopy(event: ClipboardEvent) {
  if (!markdownText.value || !event.clipboardData) {
    return;
  }

  event.clipboardData.setData('text/plain', markdownText.value);
  event.preventDefault();
  feedback.showToast('Copied');
}

function formatDate(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString();
}
</script>

<style scoped>
.oauth-token-audit-report {
  display: grid;
  flex: 1 1 auto;
  gap: 1rem;
  min-height: 0;
  min-width: 0;
  border: 1px solid var(--bo-border-soft);
  border-radius: 10px;
  padding: 0.8rem;
  background: var(--bo-surface-panel);
  color: var(--bo-ink-default);
  overflow-y: auto;
  user-select: text;
}

.oauth-token-audit-report h4,
.oauth-token-audit-report h5,
.oauth-token-audit-report dl {
  margin: 0;
}

.oauth-token-audit-report h4 {
  font-size: 1rem;
}

.oauth-token-audit-report h5 {
  margin-bottom: 0.35rem;
  font-size: 0.9rem;
}

.oauth-token-audit-report-facts {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(12rem, 1fr));
  gap: 0.55rem 0.9rem;
}

.oauth-token-audit-report-facts div {
  min-width: 0;
}

.oauth-token-audit-report-facts dt {
  color: var(--bo-ink-muted);
  font-size: 0.76rem;
  font-weight: 800;
  text-transform: uppercase;
}

.oauth-token-audit-report-facts dd {
  margin: 0.1rem 0 0;
  overflow-wrap: anywhere;
}
</style>
