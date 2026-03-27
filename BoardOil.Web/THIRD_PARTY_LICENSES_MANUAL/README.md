# Manual Third-Party Licence Overrides

Use this folder for packages that do not ship a standalone licence file in `node_modules`.

The sync script (`npm run sync:third-party-licences`) checks here using the generated output filename for each package:

- `npm-<package-name>.txt`

Examples:

- `@microsoft/signalr` -> `npm-microsoft-signalr.txt`
- `vue-router` -> `npm-vue-router.txt`

Current expected manual override file:

- `npm-microsoft-signalr.txt`

When present, the file is copied into `BoardOil.Web/THIRD_PARTY_LICENSES` and treated as resolved.
