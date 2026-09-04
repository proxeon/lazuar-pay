// Multi-image build definitions for Lazuar Pay → GHCR.
// This repo is focused Pay only: host, merchant, checkout.
// (Hub museum images live in the old branch, not here.)

variable "REGISTRY" {
  default = "ghcr.io/proxeon"
}

variable "TAG" {
  default = "latest"
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
  targets = ["lazuar-pay", "lazuar-pay-api", "lazuar-pay-merchant", "lazuar-pay-checkout"]
}

target "docker-metadata-action" {}

target "_common" {
  platforms = [PLATFORMS]
  labels = {
    "org.opencontainers.image.source"      = "https://github.com/proxeon/lazuar-pay"
    "org.opencontainers.image.vendor"      = "Lazuar"
    "org.opencontainers.image.description" = "Lazuar Pay CaaS"
  }
}

target "lazuar-pay-api" {
  inherits   = ["_common"]
  context    = "."
  dockerfile = "apps/lazuar-api/Dockerfile"
  tags = [
    "${REGISTRY}/lazuar-pay-api:${TAG}",
    "${REGISTRY}/lazuar-pay-api:latest",
  ]
  labels = {
    "org.opencontainers.image.title" = "lazuar-pay-api"
  }
}

# Checklist name for the Rust image. Keep `lazuar-pay` (C#) until Phase 8 + 30 days.
target "pay-api" {
  inherits = ["lazuar-pay-api"]
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
    VITE_PAY_API_URL                      = VITE_PAY_API_URL
    VITE_CHECKOUT_ORIGIN                  = VITE_CHECKOUT_ORIGIN
    VITE_ONE_API_URL                      = VITE_ONE_API_URL
    VITE_ZITADEL_AUTHORITY                = VITE_ZITADEL_AUTHORITY
    VITE_ZITADEL_CLIENT_ID                = VITE_ZITADEL_CLIENT_ID
    VITE_ZITADEL_REDIRECT_URI             = VITE_ZITADEL_REDIRECT_URI
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
