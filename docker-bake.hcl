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

variable "NEXT_PUBLIC_OPS_URL" {
  default = "https://hub.lazuar.com"
}

variable "VITE_BASE_PATH_ADMIN" {
  default = "/admin/"
}

variable "PLATFORMS" {
  default = "linux/amd64"
}

variable "VITE_PAY_API_URL" {
  default = ""
}

variable "VITE_CHECKOUT_ORIGIN" {
  default = ""
}

variable "VITE_ONE_API_URL" {
  default = ""
}

variable "VITE_ZITADEL_AUTHORITY" {
  default = ""
}

variable "VITE_ZITADEL_CLIENT_ID" {
  default = ""
}

variable "VITE_ZITADEL_REDIRECT_URI" {
  default = ""
}

variable "VITE_ZITADEL_POST_LOGOUT_REDIRECT_URI" {
  default = ""
}

group "default" {
  targets = ["api", "lazuar-portal", "lazuar-ops", "lazuar-admin", "lazuar-developers"]
}

# Focused Pay. Not Hub. Bake separately: `docker buildx bake pay`
group "pay" {
  targets = ["lazuar-pay", "lazuar-pay-merchant", "lazuar-pay-checkout"]
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

target "lazuar-portal" {
  inherits   = ["_common"]
  context    = "."
  dockerfile = "apps/lazuar-portal/Dockerfile"
  tags = [
    "${REGISTRY}/lazuar-hub-portal:${TAG}",
    "${REGISTRY}/lazuar-hub-portal:latest",
  ]
  args = {
    NEXT_PUBLIC_API_URL = NEXT_PUBLIC_API_URL
    NEXT_BASE_PATH      = NEXT_BASE_PATH
    NEXT_PUBLIC_OPS_URL = NEXT_PUBLIC_OPS_URL
  }
  labels = {
    "org.opencontainers.image.title" = "lazuar-hub-portal"
  }
}

target "lazuar-ops" {
  inherits   = ["_common"]
  context    = "."
  dockerfile = "apps/lazuar-ops/Dockerfile"
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

target "lazuar-admin" {
  inherits   = ["_common"]
  context    = "."
  dockerfile = "apps/lazuar-admin/Dockerfile"
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

target "lazuar-developers" {
  inherits   = ["_common"]
  context    = "."
  dockerfile = "apps/lazuar-developers/Dockerfile"
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

target "lazuar-pay" {
  inherits   = ["_common"]
  context    = "."
  dockerfile = "apps/lazuar-pay/Dockerfile"
  tags = [
    "${REGISTRY}/lazuar-pay:${TAG}",
    "${REGISTRY}/lazuar-pay:latest",
  ]
  labels = {
    "org.opencontainers.image.title" = "lazuar-pay"
  }
}

target "lazuar-pay-merchant" {
  inherits   = ["_common"]
  context    = "."
  dockerfile = "apps/lazuar-pay-merchant/Dockerfile"
  tags = [
    "${REGISTRY}/lazuar-pay-merchant:${TAG}",
    "${REGISTRY}/lazuar-pay-merchant:latest",
  ]
  args = {
    VITE_PAY_API_URL                     = VITE_PAY_API_URL
    VITE_CHECKOUT_ORIGIN                 = VITE_CHECKOUT_ORIGIN
    VITE_ONE_API_URL                     = VITE_ONE_API_URL
    VITE_ZITADEL_AUTHORITY               = VITE_ZITADEL_AUTHORITY
    VITE_ZITADEL_CLIENT_ID               = VITE_ZITADEL_CLIENT_ID
    VITE_ZITADEL_REDIRECT_URI            = VITE_ZITADEL_REDIRECT_URI
    VITE_ZITADEL_POST_LOGOUT_REDIRECT_URI = VITE_ZITADEL_POST_LOGOUT_REDIRECT_URI
  }
  labels = {
    "org.opencontainers.image.title" = "lazuar-pay-merchant"
  }
}

target "lazuar-pay-checkout" {
  inherits   = ["_common"]
  context    = "."
  dockerfile = "apps/lazuar-pay-checkout/Dockerfile"
  tags = [
    "${REGISTRY}/lazuar-pay-checkout:${TAG}",
    "${REGISTRY}/lazuar-pay-checkout:latest",
  ]
  args = {
    VITE_PAY_API_URL = VITE_PAY_API_URL
  }
  labels = {
    "org.opencontainers.image.title" = "lazuar-pay-checkout"
  }
}
