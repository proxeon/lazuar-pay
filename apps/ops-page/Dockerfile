# syntax=docker/dockerfile:1

# Vite SPA → static dist, served by a tiny Node static server (no nginx/Caddy).
# Host reverse proxy (Caddy on Ubuntu) points at this container's port.

FROM node:22-alpine AS build
RUN corepack enable && corepack prepare pnpm@11.5.2 --activate
WORKDIR /app

COPY package.json pnpm-lock.yaml pnpm-workspace.yaml turbo.json ./
COPY packages/api-types-ts/package.json packages/api-types-ts/
COPY packages/typescript-config/package.json packages/typescript-config/
COPY apps/ops-page/package.json apps/ops-page/

RUN pnpm install --filter ./apps/ops-page... --frozen-lockfile

COPY packages/api-types-ts packages/api-types-ts
COPY packages/typescript-config packages/typescript-config
COPY apps/ops-page apps/ops-page

ARG VITE_API_URL=http://localhost:8080/api/v1
ARG VITE_PORTAL_URL=http://localhost:3004
ENV VITE_API_URL=$VITE_API_URL
ENV VITE_PORTAL_URL=$VITE_PORTAL_URL

RUN pnpm --filter ./apps/ops-page build

FROM node:22-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production
RUN npm install -g serve@14.2.4 \
  && addgroup -g 1001 web \
  && adduser -D -u 1001 -G web web
COPY --from=build --chown=web:web /app/apps/ops-page/dist ./dist
USER web
EXPOSE 3000
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget -qO- http://127.0.0.1:3000/ || exit 1
# -s: SPA fallback to index.html
CMD ["serve", "-s", "dist", "-l", "3000"]
