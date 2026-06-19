import fs from "fs";
import path from "path";
import { ApiReference } from "@scalar/nextjs-api-reference";

const specPath = path.join(process.cwd(), "../../packages/api-spec/dist/lhdn/openapi.yaml");
const openapiSpec = fs.readFileSync(specPath, "utf8");

export const GET = ApiReference({
  spec: {
    content: openapiSpec,
  },
  theme: "default",
  metaData: {
    title: "Lazuar LHDN API",
  },
});
