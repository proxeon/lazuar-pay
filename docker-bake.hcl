// Multi-image build definitions for Lazuar Hub → GHCR
// Images are ALWAYS built for linux/amd64 so they run on Ubuntu servers.
//
// Only runtime apps are containerized:
//   - api          (.NET)
//   - portal-page  (Next.js Node server)
//
// Vite SPAs (ops-page, superadmin-page) are static assets — serve them
// from the host Caddyfile on the Ubuntu server (file_server), not from
// nginx/Caddy containers.
//
// Usage:
//   docker buildx bake --push
//   TAG=$(git rev-parse --short HEAD) docker buildx bake --push

variable "REGISTRY" {
  default = "ghcr.io/proxeon"
}

variable "TAG" {
  default = "latest"
}

variable "NEXT_PUBLIC_API_URL" {
  default = "http://localhost:8080/api/v1"
}

// Target platform for production Ubuntu servers (x86_64).
variable "PLATFORMS" {
  default = "linux/amd64"
}

group "default" {
  targets = ["api", "portal-page"]
}

target "docker-metadata-action" {}

target "_common" {
  platforms = [PLATFORMS]
  labels = {
    "org.opencontainers.image.source"      = "https://github.com/proxeon/lazuar-hub"
    "org.opencontainers.image.vendor"      = "Lazuar"
    "org.opencontainers.image.description" = "Lazuar Hub CaaS platform"
  }
}

target "api" {
  inherits   = ["_common"]
  context    = "."
  dockerfile = "apps/lazuar-api/Dockerfile"
  tags = [
    "${REGISTRY}/lazuar-hub/api:${TAG}",
    "${REGISTRY}/lazuar-hub/api:latest",
  ]
  labels = {
    "org.opencontainers.image.title" = "lazuar-hub/api"
  }
}

target "portal-page" {
  inherits   = ["_common"]
  context    = "."
  dockerfile = "apps/portal-page/Dockerfile"
  tags = [
    "${REGISTRY}/lazuar-hub/portal-page:${TAG}",
    "${REGISTRY}/lazuar-hub/portal-page:latest",
  ]
  args = {
    NEXT_PUBLIC_API_URL = NEXT_PUBLIC_API_URL
  }
  labels = {
    "org.opencontainers.image.title" = "lazuar-hub/portal-page"
  }
}
