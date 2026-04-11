/*
Example lint error:
getEnvelope('/api/auth/me');
getEnvelope('/api/system/users');

ESLint error:
boardoil/api-path-single-prefix: API clients should target a single /api/* prefix per file. Found: /api/auth, /api/system.

Fix: split calls into separate API client files.
*/
const apiPrefixPattern = /^\/api\/([^/]+)/;

function resolveApiPrefix(value) {
  if (typeof value !== 'string') {
    return null;
  }

  const match = apiPrefixPattern.exec(value);
  if (!match) {
    return null;
  }

  return `/api/${match[1]}`;
}

const apiPathSinglePrefixRule = {
  meta: {
    type: 'problem',
    docs: {
      description: 'Enforce a single /api/* prefix per API client file.'
    },
    schema: [],
    messages: {
      multiplePrefixes: 'API clients should target a single /api/* prefix per file. Found: {{prefixes}}.'
    }
  },
  create(context) {
    const prefixes = new Set();

    function addPrefix(value) {
      const prefix = resolveApiPrefix(value);
      if (prefix) {
        prefixes.add(prefix);
      }
    }

    return {
      Literal(node) {
        addPrefix(node.value);
      },
      TemplateLiteral(node) {
        const head = node.quasis[0];
        if (!head) {
          return;
        }

        const rawValue = head.value.cooked ?? head.value.raw;
        addPrefix(rawValue);
      },
      'Program:exit'(node) {
        if (prefixes.size <= 1) {
          return;
        }

        context.report({
          node,
          messageId: 'multiplePrefixes',
          data: {
            prefixes: Array.from(prefixes).sort().join(', ')
          }
        });
      }
    };
  }
};

const plugin = {
  rules: {
    'api-path-single-prefix': apiPathSinglePrefixRule
  }
};

export default plugin;
