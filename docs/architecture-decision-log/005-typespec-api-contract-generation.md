
# 005: TypeSpec API Contract & Code Generation Pipeline

**Date:** January 2025  
**Status:** Accepted  
**Context:**  
As the Lazuar Modular Monolith scales, manually synchronizing C# Minimal API DTOs with frontend TypeScript interfaces has become a major source of bugs (e.g., payload mismatches like `template_name` vs `TemplateId` causing 500 Internal Server Errors). To guarantee end-to-end type safety without tightly coupling our frontend repositories to our backend compilation process, we need a "Contract-First" approach.

## Decision
We have adopted **TypeSpec** as the single source of truth for our API contracts. From TypeSpec, we generate an OpenAPI v3 specification, which is then used to automatically generate both frontend TypeScript definitions and backend .NET DTO records.

To maintain strict physical boundaries, this pipeline is isolated entirely within the `packages/` directory of our PNPM monorepo.

## How the Pipeline Works

The entire pipeline is orchestrated via `Taskfile.yml` using the command:
```bash
task gen
```
When executed, the pipeline runs in three distinct phases:

### 1. Specification Compilation (`task gen:spec`)
* **Source of Truth:** Developers write API contracts in `packages/api-spec/main.tsp`.
* **Action:** The TypeSpec compiler (`tsp`) reads the `.tsp` files.
* **Output:** Generates a standard OpenAPI v3 specification file at:
  `packages/api-spec/dist/openapi.yaml`
* *Note: The `dist/` directory is ignored in git. The YAML is treated purely as a transient build artifact.*

### 2. TypeScript Generation (`task gen:types-ts`)
* **Action:** Uses `openapi-typescript` to read the transient `openapi.yaml`.
* **Output:** Generates strictly typed, dependency-free TypeScript definitions at:
  `packages/api-types-ts/src/index.ts`
* **Consumption:** This package acts as an internal NPM module (`@repo/api-types-ts`). Our frontends (`community-admin`, `community-page`) consume it via PNPM workspace dependencies, allowing them to type-check `fetch` requests with zero runtime overhead.

### 3. .NET C# Generation (`task gen:types-dotnet`)
* **Action:** Uses `NSwag.ConsoleCore` (installed via local `.config/dotnet-tools.json`) to read the transient `openapi.yaml`.
* **Output:** Generates C# DTO types at:
  `packages/api-types-dotnet/Lazuar.ApiContracts.cs`
* **Consumption:** This file is compiled by an isolated C# project (`Lazuar.ApiContracts.csproj` with `EnableDefaultCompileItems=false` so only this single generated file is included). It is attached to the backend `.slnx` solution. Backend modules (e.g., Payments, Lhdn) reference this project to map internal Domain Entities to strictly defined API input/output models.

## Architectural Guidelines & Rules

1. **Never edit generated files:** You must never manually edit `src/index.ts`, `Lazuar.ApiContracts.cs`, or `openapi.yaml`. All changes must be made in `api-spec/main.tsp` and regenerated using `task gen`.
2. **Commit generated code:** Both `index.ts` and `Lazuar.ApiContracts.cs` should be committed to version control. This ensures that frontend and backend developers can pull the repo and run the apps without being forced to install the TypeSpec compiler locally.
3. **No Domain Leaks:** The generated .NET DTOs represent the *external contract*. They must not be used as database entities or aggregate roots. Always map incoming DTOs to Commands/Queries at the Application layer.
4. **Isolated Tooling:** The NSwag CLI tool is localized via `packages/api-types-dotnet/.config/dotnet-tools.json`. This guarantees that CI/CD pipelines and other developers always execute the exact same version of the code generator without requiring global machine installations.
