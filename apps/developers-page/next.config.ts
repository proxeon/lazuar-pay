import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  // Production: https://hub.lazuar.com/docs
  basePath: process.env.NEXT_BASE_PATH || "",
};

export default nextConfig;
