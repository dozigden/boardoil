import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createSystemApi } from '../../shared/api/systemApi';
import type {
  CreateMcpProjectConnectionRequest,
  McpProjectConnection
} from '../../shared/types/mcpProjectConnectionTypes';

export const useSystemProjectConnectionsStore = defineStore('systemProjectConnections', () => {
  const connections = ref<McpProjectConnection[]>([]);
  const busy = ref(false);
  const errorMessage = ref<string | null>(null);
  const successMessage = ref<string | null>(null);
  const api = createSystemApi();

  function clearMessages() {
    errorMessage.value = null;
    successMessage.value = null;
  }

  async function loadConnections() {
    busy.value = true;
    errorMessage.value = null;
    try {
      const result = await api.getMcpProjectConnections();
      if (!result.ok) {
        errorMessage.value = result.error.message;
        connections.value = [];
        return false;
      }

      connections.value = result.data;
      return true;
    } finally {
      busy.value = false;
    }
  }

  async function createConnection(
    request: CreateMcpProjectConnectionRequest
  ): Promise<McpProjectConnection | null> {
    busy.value = true;
    clearMessages();
    try {
      const result = await api.createMcpProjectConnection(request);
      if (!result.ok) {
        errorMessage.value = result.error.message;
        return null;
      }

      connections.value = sortConnections([...connections.value, result.data]);
      successMessage.value = `Created project connection ${result.data.name}.`;
      return result.data;
    } finally {
      busy.value = false;
    }
  }

  async function revokeConnection(connection: McpProjectConnection) {
    busy.value = true;
    clearMessages();
    try {
      const revokeResult = await api.revokeMcpProjectConnection(connection.id);
      if (!revokeResult.ok) {
        errorMessage.value = revokeResult.error.message;
        return false;
      }

      const refreshResult = await api.getMcpProjectConnections();
      if (!refreshResult.ok) {
        errorMessage.value = refreshResult.error.message;
        return false;
      }

      connections.value = refreshResult.data;
      successMessage.value = `Revoked project connection ${connection.name}.`;
      return true;
    } finally {
      busy.value = false;
    }
  }

  return {
    connections,
    busy,
    errorMessage,
    successMessage,
    clearMessages,
    loadConnections,
    createConnection,
    revokeConnection
  };
});

function sortConnections(connections: McpProjectConnection[]) {
  return [...connections].sort((left, right) => {
    if (left.isActive !== right.isActive) {
      return left.isActive ? -1 : 1;
    }

    const nameComparison = left.name.localeCompare(right.name);
    if (nameComparison !== 0) {
      return nameComparison;
    }

    return left.id - right.id;
  });
}
