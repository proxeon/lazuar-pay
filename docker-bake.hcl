// Multi-image build definitions for Lazuar Hub → GHCR
// Flat image names (avoids nested-package 403 from GITHUB_TOKEN):
//   ghcr.io/proxeon/lazuar-hub-api
//   ghcr.io/proxeon/lazuar-hub-ops
//   ghcr.io/proxeon/lazuar-hub-portal
//   ghcr.io/proxeon/lazuar-hub-superadmin
//   ghcr.io/proxeon/lazuar-hub-developers
//
// Public paths:
//   https://hub.lazuar.com/           ops
//   https://hub.lazuar.com/portal     portal
//   https://hub.lazuar.com/docs       developers
//   https://hub.lazuar.com/api/v1     api
//   https://hub.lazuar.com/admin      superadmin

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
  targets = ["api", "portal-page", "ops-page", "superadmin-page", "developers-page"]
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
    "${REGISTRY}/lazuar-hub-api:${TAG}",
    "${REGISTRY}/lazuar-hub-api:latest",
  ]
  labels = {
    "org.opencontainers.image.title" = "lazuar-hub-api"
  }
}

target "portal-page" {
  inherits   = ["_common"]
  context    = "."
  dockerfile = "apps/portal-page/Dockerfile"
  tags = [
    "${REGISTRY}/lazuar-hub-portal:${TAG}",
    "${REGISTRY}/lazuar-hub-portal:latest",
  ]
  args = {
    NEXT_PUBLIC_API_URL = NEXT_PUBLIC_API_URL
    NEXT_BASE_PATH      = NEXT_BASE_PATH
  }
  labels = {
    "org.opencontainers.image.title" = "lazuar-hub-portal"
  }
}

target "ops-page" {
  inherits   = ["_common"]
  context    = "."
  dockerfile = "apps/ops-page/Dockerfile"
  tags = [
    "${REGISTRY}/lazuar-hub-ops:${TAG}",
    "${REGISTRY}/lazuar-hub-ops:latest",
  ]
  args = {
    VITE_API_URL    = VITE_API_URL
    VITE_PORTAL_URL = VITE_PORTAL_URL
    VITE_BASE_PATH  = "/"
  }
  labels = {
    "org.opencontainers.image.title" = "lazuar-hub-ops"
  }
}

target "superadmin-page" {
  inherits   = ["_common"]
  context    = "."
  dockerfile = "apps/superadmin-page/Dockerfile"
  tags = [
    "${REGISTRY}/lazuar-hub-superadmin:${TAG}",
    "${REGISTRY}/lazuar-hub-superadmin:latest",
  ]
  args = {
    VITE_API_URL   = VITE_API_URL
    VITE_BASE_PATH = VITE_BASE_PATH_ADMIN
  }
  labels = {
    "org.opencontainers.image.title" = "lazuar-hub-superadmin"
  }
}

target "developers-page" {
  inherits   = ["_common"]
  context    = "."
  dockerfile = "apps/developers-page/Dockerfile"
  tags = [
    "${REGISTRY}/lazuar-hub-developers:${TAG}",
    "${REGISTRY}/lazuar-hub-developers:latest",
  ]
  args = {
    NEXT_BASE_PATH = "/docs"
  }
  labels = {
    "org.opencontainers.image.title" = "lazuar-hub-developers"
  }
}
