import fs from "fs";
import path from "path";

/**
 * Resolve OpenAPI YAML for Scalar routes.
 * Local monorepo: packages/api-spec/dist/<module>/openapi.yaml
 * Docker: OPENAPI_SPEC_ROOT=/app/openapi-specs
 */
export function readOpenApiSpec(moduleDir: string): string {
  const root =
    process.env.OPENAPI_SPEC_ROOT ||
    path.join(process.cwd(), "../../packages/api-spec/dist");
  const specPath = path.join(root, moduleDir, "openapi.yaml");
  if (!fs.existsSync(specPath)) {
    throw new Error(`OpenAPI spec not found: ${specPath}`);
  }
  return fs.readFileSync(specPath, "utf8");
}
