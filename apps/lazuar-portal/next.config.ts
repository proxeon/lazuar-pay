import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Required for slim Docker images (copies only needed server files)
  output: "standalone",
  // Production: https://hub.lazuar.com/portal  (local dev: leave unset)
  basePath: process.env.NEXT_BASE_PATH || "",
};

export default nextConfig;
