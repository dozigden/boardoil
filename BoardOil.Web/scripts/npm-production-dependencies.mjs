import path from "node:path";

function compareText(left, right) {
  if (left < right) {
    return -1;
  }

  if (left > right) {
    return 1;
  }

  return 0;
}

function isObject(value) {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

function getDependencyEdges(node) {
  const edgesByName = new Map();

  for (const dependencyName of Object.keys(node._dependencies ?? {})) {
    edgesByName.set(dependencyName, {
      dependencyName,
      optional: false
    });
  }

  for (const dependencyName of Object.keys(node.optionalDependencies ?? {})) {
    edgesByName.set(dependencyName, {
      dependencyName,
      optional: true
    });
  }

  for (const dependencyName of Object.keys(node.peerDependencies ?? {})) {
    if (node.peerDependenciesMeta?.[dependencyName]?.optional === true) {
      continue;
    }

    edgesByName.set(dependencyName, {
      dependencyName,
      optional: false
    });
  }

  return [...edgesByName.values()].sort((left, right) =>
    compareText(left.dependencyName, right.dependencyName)
  );
}

function getNodeDescription(node) {
  const name = typeof node.name === "string" ? node.name : "project root";
  const version = typeof node.version === "string" ? `@${node.version}` : "";
  return `${name}${version}`;
}

function getNodePath(node, projectRoot) {
  if (typeof node.path === "string" && node.path.length > 0) {
    return path.resolve(node.path);
  }

  return path.resolve(projectRoot);
}

function buildNodesByPath(tree) {
  const nodesByPath = new Map();
  const queue = [tree];

  for (let queueIndex = 0; queueIndex < queue.length; queueIndex += 1) {
    const node = queue[queueIndex];
    if (!isObject(node)) {
      continue;
    }

    if (typeof node.path === "string") {
      const nodePath = path.resolve(node.path);
      let indexedNode = nodesByPath.get(nodePath);
      if (!indexedNode) {
        indexedNode = {
          ...node,
          dependencies: {}
        };
        nodesByPath.set(nodePath, indexedNode);
      }

      Object.assign(indexedNode.dependencies, isObject(node.dependencies) ? node.dependencies : {});
    }

    if (isObject(node.dependencies)) {
      queue.push(...Object.values(node.dependencies));
    }
  }

  return nodesByPath;
}

function resolveDependencyNode(parent, dependencyName, projectRoot, nodesByPath) {
  const nestedChild = isObject(parent.dependencies) ? parent.dependencies[dependencyName] : null;
  if (isObject(nestedChild)) {
    if (typeof nestedChild.path === "string") {
      return nodesByPath.get(path.resolve(nestedChild.path)) ?? nestedChild;
    }

    return nestedChild;
  }

  let searchRoot = getNodePath(parent, projectRoot);
  const dependencyPathParts = dependencyName.split("/");

  while (true) {
    const candidatePath = path.join(searchRoot, "node_modules", ...dependencyPathParts);
    const indexedNode = nodesByPath.get(candidatePath);
    if (indexedNode) {
      return indexedNode;
    }

    const parentSearchRoot = path.dirname(searchRoot);
    if (parentSearchRoot === searchRoot) {
      return null;
    }

    searchRoot = parentSearchRoot;
  }
}

function buildMissingDependencyIssue(parent, edge, projectRoot) {
  const parentPath = getNodePath(parent, projectRoot);
  const relativeParentPath = path.relative(projectRoot, parentPath) || ".";

  return {
    packageName: edge.dependencyName,
    version: "unknown",
    reason:
      `Resolved npm production graph is missing required dependency ${edge.dependencyName} ` +
      `declared by ${getNodeDescription(parent)} at ${relativeParentPath}`,
    expectedSourceFile: "package-lock.json",
    resolutionHint: "Run npm ci to restore the exact locked install, then regenerate licences from"
  };
}

export function collectProductionNpmDependencies(tree, projectRoot) {
  if (!isObject(tree)) {
    return {
      packages: [],
      sourceIssues: [
        {
          packageName: "npm production dependency graph",
          version: "unknown",
          reason: "npm ls did not return an object dependency graph",
          expectedSourceFile: "package-lock.json",
          resolutionHint: "Run npm ci to restore the exact locked install, then regenerate licences from"
        }
      ]
    };
  }

  const packagesByKey = new Map();
  const sourceIssues = [];
  const expandedPaths = new Set();
  const nodesByPath = buildNodesByPath(tree);
  const queue = getDependencyEdges(tree).map(edge => ({ parent: tree, edge }));

  for (let queueIndex = 0; queueIndex < queue.length; queueIndex += 1) {
    const { parent, edge } = queue[queueIndex];
    const child = resolveDependencyNode(parent, edge.dependencyName, projectRoot, nodesByPath);

    if (!isObject(child)) {
      if (!edge.optional) {
        sourceIssues.push(buildMissingDependencyIssue(parent, edge, projectRoot));
      }
      continue;
    }

    const packageName = typeof child.name === "string" ? child.name : edge.dependencyName;
    const version = typeof child.version === "string" ? child.version : null;
    const packageRoot = typeof child.path === "string" ? path.resolve(child.path) : null;

    if (!version || !packageRoot) {
      if (!edge.optional) {
        sourceIssues.push(buildMissingDependencyIssue(parent, edge, projectRoot));
      }
      continue;
    }

    const packageKey = `${packageName}\u0000${version}`;
    let npmPackage = packagesByKey.get(packageKey);
    if (!npmPackage) {
      npmPackage = {
        packageName,
        version,
        packageRoots: new Set()
      };
      packagesByKey.set(packageKey, npmPackage);
    }
    npmPackage.packageRoots.add(packageRoot);

    if (expandedPaths.has(packageRoot)) {
      continue;
    }
    expandedPaths.add(packageRoot);

    for (const childEdge of getDependencyEdges(child)) {
      queue.push({ parent: child, edge: childEdge });
    }
  }

  const packages = [...packagesByKey.values()]
    .map(npmPackage => ({
      packageName: npmPackage.packageName,
      version: npmPackage.version,
      packageRoots: [...npmPackage.packageRoots].sort(compareText)
    }))
    .sort((left, right) => {
      const byName = compareText(left.packageName, right.packageName);
      if (byName !== 0) {
        return byName;
      }

      return compareText(left.version, right.version);
    });

  sourceIssues.sort((left, right) => {
    const byName = compareText(left.packageName, right.packageName);
    if (byName !== 0) {
      return byName;
    }

    return compareText(left.reason, right.reason);
  });

  return { packages, sourceIssues };
}
