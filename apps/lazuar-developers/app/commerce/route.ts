import { ApiReference } from "@scalar/nextjs-api-reference";
import { readOpenApiSpec } from "../../lib/openapi";

const openapiSpec = readOpenApiSpec("commerce");

export const GET = ApiReference({
  spec: {
    content: openapiSpec,
  },
  theme: "default",
  metaData: {
    title: "Lazuar Commerce API",
  },
});
