// Multi-image build definitions for Lazuar Hub → GHCR
// Images are ALWAYS built for linux/amd64 so they run on Ubuntu servers.
//
// Public paths (single host):
//   https://hub.lazuar.com/           ops
//   https://hub.lazuar.com/portal     portal
//   https://hub.lazuar.com/api/v1     api
//   https://hub.lazuar.com/admin      superadmin
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
  default = "https://hub.lazuar.com/api/v1"
}

variable "VITE_PORTAL_URL" {
  default = "https://hub.lazuar.com/portal"
}

variable "NEXT_PUBLIC_API_URL" {
  default = "https://hub.lazuar.com/api/v1"
}

variable "NEXT_BASE_PATH" {
  default = "/portal"
}

variable "VITE_BASE_PATH_ADMIN" {
  default = "/admin/"
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
    NEXT_BASE_PATH      = NEXT_BASE_PATH
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
    VITE_BASE_PATH  = "/"
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
    VITE_API_URL   = VITE_API_URL
    VITE_BASE_PATH = VITE_BASE_PATH_ADMIN
  }
  labels = {
    "org.opencontainers.image.title" = "lazuar-hub/superadmin-page"
  }
}
