// Multi-image build definitions for Lazuar Hub → GHCR
// Images are ALWAYS built for linux/amd64 so they run on Ubuntu servers.
//
// Runtime images (pull on server; host Caddy reverse_proxies):
//   - api              .NET :8080
//   - portal-page      Next.js :3000
//   - ops-page         Vite SPA via serve :3000
//   - superadmin-page  Vite SPA via serve :3000
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

variable "VITE_API_URL" {
  default = "http://localhost:8080/api/v1"
}

variable "VITE_PORTAL_URL" {
  default = "http://localhost:3004"
}

variable "NEXT_PUBLIC_API_URL" {
  default = "http://localhost:8080/api/v1"
}

variable "PLATFORMS" {
  default = "linux/amd64"
}

group "default" {
  targets = ["api", "portal-page", "ops-page", "superadmin-page"]
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

target "ops-page" {
  inherits   = ["_common"]
  context    = "."
  dockerfile = "apps/ops-page/Dockerfile"
  tags = [
    "${REGISTRY}/lazuar-hub/ops-page:${TAG}",
    "${REGISTRY}/lazuar-hub/ops-page:latest",
  ]
  args = {
    VITE_API_URL    = VITE_API_URL
    VITE_PORTAL_URL = VITE_PORTAL_URL
  }
  labels = {
    "org.opencontainers.image.title" = "lazuar-hub/ops-page"
  }
}

target "superadmin-page" {
  inherits   = ["_common"]
  context    = "."
  dockerfile = "apps/superadmin-page/Dockerfile"
  tags = [
    "${REGISTRY}/lazuar-hub/superadmin-page:${TAG}",
    "${REGISTRY}/lazuar-hub/superadmin-page:latest",
  ]
  args = {
    VITE_API_URL = VITE_API_URL
  }
  labels = {
    "org.opencontainers.image.title" = "lazuar-hub/superadmin-page"
  }
}
