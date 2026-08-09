import { describe, expect, it } from "vitest";
import { collectProductionNpmDependencies } from "./npm-production-dependencies.mjs";

function packageNode(name, version, nodePath, metadata = {}) {
  return {
    name,
    version,
    path: nodePath,
    ...metadata
  };
}

describe("collectProductionNpmDependencies", () => {
  it("traverses runtime, installed optional, and required peer dependencies", () => {
    const tree = {
      name: "example",
      path: "/app",
      _dependencies: { direct: "1.0.0" },
      dependencies: {
        direct: packageNode("direct", "1.0.0", "/app/node_modules/direct", {
          _dependencies: {
            optional: "1.0.0",
            transitive: "1.0.0"
          },
          optionalDependencies: { optional: "1.0.0" },
          peerDependencies: {
            requiredPeer: "1.0.0",
            optionalPeer: "1.0.0"
          },
          peerDependenciesMeta: {
            optionalPeer: { optional: true }
          },
          dependencies: {
            optional: packageNode("optional", "1.0.0", "/app/node_modules/optional"),
            optionalPeer: packageNode(
              "optionalPeer",
              "1.0.0",
              "/app/node_modules/optional-peer"
            ),
            requiredPeer: packageNode(
              "requiredPeer",
              "1.0.0",
              "/app/node_modules/required-peer"
            ),
            transitive: packageNode(
              "transitive",
              "1.0.0",
              "/app/node_modules/transitive"
            )
          }
        }),
        devOnly: packageNode("devOnly", "1.0.0", "/app/node_modules/dev-only")
      }
    };

    const result = collectProductionNpmDependencies(tree, "/app");

    expect(result.sourceIssues).toEqual([]);
    expect(result.packages.map(npmPackage => npmPackage.packageName)).toEqual([
      "direct",
      "optional",
      "requiredPeer",
      "transitive"
    ]);
  });

  it("reports missing required dependencies and ignores missing optional dependencies", () => {
    const tree = {
      name: "example",
      path: "/app",
      _dependencies: { direct: "1.0.0" },
      dependencies: {
        direct: packageNode("direct", "1.0.0", "/app/node_modules/direct", {
          _dependencies: {
            missingOptional: "1.0.0",
            missingRequired: "1.0.0"
          },
          optionalDependencies: { missingOptional: "1.0.0" },
          dependencies: {}
        })
      }
    };

    const result = collectProductionNpmDependencies(tree, "/app");

    expect(result.sourceIssues).toHaveLength(1);
    expect(result.sourceIssues[0]).toMatchObject({
      packageName: "missingRequired",
      expectedSourceFile: "package-lock.json"
    });
    expect(result.sourceIssues[0].reason).toContain("declared by direct@1.0.0");
  });

  it("deduplicates identical versions across paths while retaining distinct versions", () => {
    const tree = {
      name: "example",
      path: "/app",
      _dependencies: { first: "1.0.0", second: "1.0.0" },
      dependencies: {
        first: packageNode("shared", "1.0.0", "/app/node_modules/first"),
        second: packageNode("shared", "1.0.0", "/app/node_modules/second", {
          _dependencies: { older: "0.9.0" },
          dependencies: {
            older: packageNode("shared", "0.9.0", "/app/node_modules/second/node_modules/shared")
          }
        })
      }
    };

    const result = collectProductionNpmDependencies(tree, "/app");

    expect(result.packages).toEqual([
      {
        packageName: "shared",
        version: "0.9.0",
        packageRoots: ["/app/node_modules/second/node_modules/shared"]
      },
      {
        packageName: "shared",
        version: "1.0.0",
        packageRoots: ["/app/node_modules/first", "/app/node_modules/second"]
      }
    ]);
  });

  it("resolves dependencies omitted from a deduplicated npm ls node", () => {
    const parser = packageNode("parser", "1.0.0", "/app/node_modules/parser");
    const compiler = packageNode("compiler", "1.0.0", "/app/node_modules/compiler", {
      _dependencies: { parser: "1.0.0" }
    });
    const tree = {
      name: "example",
      path: "/app",
      _dependencies: { compiler: "1.0.0", route: "1.0.0" },
      dependencies: {
        compiler,
        route: packageNode("route", "1.0.0", "/app/node_modules/route", {
          _dependencies: { compiler: "1.0.0" },
          dependencies: {
            compiler: {
              ...compiler,
              dependencies: { parser }
            }
          }
        })
      }
    };

    const result = collectProductionNpmDependencies(tree, "/app");

    expect(result.sourceIssues).toEqual([]);
    expect(result.packages.map(npmPackage => npmPackage.packageName)).toEqual([
      "compiler",
      "parser",
      "route"
    ]);
  });

  it("returns deterministic output regardless of dependency object insertion order", () => {
    const createTree = reverse => {
      const dependencyEntries = [
        ["alpha", packageNode("alpha", "2.0.0", "/app/node_modules/alpha")],
        ["zulu", packageNode("zulu", "1.0.0", "/app/node_modules/zulu")]
      ];

      if (reverse) {
        dependencyEntries.reverse();
      }

      return {
        name: "example",
        path: "/app",
        _dependencies: Object.fromEntries(dependencyEntries.map(([name]) => [name, "*"])),
        dependencies: Object.fromEntries(dependencyEntries)
      };
    };

    expect(collectProductionNpmDependencies(createTree(false), "/app")).toEqual(
      collectProductionNpmDependencies(createTree(true), "/app")
    );
  });
});
