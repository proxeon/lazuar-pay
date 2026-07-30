import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Required for slim Docker images (copies only needed server files)
  output: "standalone",
};

export default nextConfig;
