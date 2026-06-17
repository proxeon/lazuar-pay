
# 011 — SDK Publishing Runbook

**Status:** Active  
**Context:** This runbook defines the exact operational steps required to compile, package, and publish the Lazuar LHDN SDKs to public package managers (NPM and NuGet).

---

## 1. Pre-Publish Checklist

Before publishing, ensure the following steps are complete:
1. **Generate the latest clients:** Run `task gen` at the monorepo root to ensure the SDK code perfectly matches the current TypeSpec definitions.
2. **Version Bump:** Update the version numbers in:
   * `packages/lhdn-sdk-ts/package.json` (`"version": "x.y.z"`)
   * `packages/lhdn-sdk-dotnet/Lazuar.Lhdn.Sdk.csproj` (`<Version>x.y.z</Version>`)
3. **Commit Changes:** Ensure all generated files and version bumps are committed to Git. `npm publish` will fail if the Git working tree is unclean.

---

## 2. Publish TypeScript SDK to NPM

The TypeScript SDK is published as a scoped public package (`@lazuar/lhdn-sdk`). 

Run these commands from the root of the repository:

```bash
# 1. Log in to your NPM account (it will prompt for username, password, and email)
npm login

# 2. Navigate to the TS SDK directory
cd packages/lhdn-sdk-ts

# 3. Ensure the project is cleanly built via TypeScript
pnpm run build

# 4. Publish to NPM
# Note: Because it is an @lazuar scoped package, --access public is strictly required.
npm publish --access public
```

*Note on Pre-releases: If you are publishing an experimental version, append `--tag next` or `--tag alpha` to the `npm publish` command to prevent it from overwriting the `latest` tag used by production customers.*

---

## 3. Publish .NET SDK to NuGet

The .NET SDK is packaged into a `.nupkg` artifact and pushed via the NuGet CLI.

### Prerequisites
1. Go to [nuget.org](https://www.nuget.org/) and log in.
2. Click your username -> **API Keys** -> **Create**.
3. Create a key with **Push** permissions.
4. Set the **Glob Pattern** to `Lazuar.*`.
5. Copy the generated key (it is only shown once).

### Execution
Run these commands from the root of the repository:

```bash
# 1. Navigate to the .NET SDK directory
cd packages/lhdn-sdk-dotnet

# 2. Compile and package the SDK into a release-ready .nupkg file
dotnet pack -c Release

# 3. Push the generated package to NuGet
# Be sure to replace the version number and API key accordingly
dotnet nuget push bin/Release/Lazuar.Lhdn.Sdk.0.1.0.nupkg \
  --api-key YOUR_NUGET_API_KEY_HERE \
  --source https://api.nuget.org/v3/index.json
```

### Post-Publish Note
Immediately after pushing to NuGet, the package will appear under **Unlisted Packages** on your NuGet account dashboard. This is standard behavior. Microsoft runs automated malware scans and indexes the package globally. It will automatically move to **Published Packages** and become available to the public within 5 to 15 minutes.
