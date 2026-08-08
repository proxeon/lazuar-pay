# syntax=docker/dockerfile:1

# ==========================================
# Build Next.js portal (standalone)
# ==========================================
FROM node:22-alpine AS build
RUN corepack enable && corepack prepare pnpm@11.5.2 --activate
WORKDIR /app

COPY package.json pnpm-lock.yaml pnpm-workspace.yaml turbo.json ./
COPY packages/api-types-ts/package.json packages/api-types-ts/
COPY packages/typescript-config/package.json packages/typescript-config/
COPY apps/portal-page/package.json apps/portal-page/

RUN pnpm install --filter ./apps/portal-page... --frozen-lockfile

COPY packages/api-types-ts packages/api-types-ts
COPY packages/typescript-config packages/typescript-config
COPY apps/portal-page apps/portal-page

ARG NEXT_PUBLIC_API_URL=https://hub.lazuar.com/api/v1
ARG NEXT_BASE_PATH=/portal
ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL
ENV NEXT_BASE_PATH=$NEXT_BASE_PATH
ENV NEXT_TELEMETRY_DISABLED=1

RUN pnpm --filter ./apps/portal-page build

# ==========================================
# Runtime
# ==========================================
FROM node:22-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
ENV PORT=3000
ENV HOSTNAME=0.0.0.0

RUN addgroup --system --gid 1001 nodejs \
  && adduser --system --uid 1001 nextjs

# Standalone server + static assets
COPY --from=build --chown=nextjs:nodejs /app/apps/portal-page/.next/standalone ./
COPY --from=build --chown=nextjs:nodejs /app/apps/portal-page/.next/static ./apps/portal-page/.next/static
COPY --from=build --chown=nextjs:nodejs /app/apps/portal-page/public ./apps/portal-page/public

USER nextjs
EXPOSE 3000
HEALTHCHECK --interval=30s --timeout=3s --start-period=15s --retries=3 \
  CMD wget -qO- http://127.0.0.1:3000/portal || exit 1

CMD ["node", "apps/portal-page/server.js"]
