# syntax=docker/dockerfile:1
# Lazuar Developer Hub (Next + Scalar OpenAPI) → hub.lazuar.com/docs

FROM node:22-alpine AS build
RUN corepack enable && corepack prepare pnpm@11.5.2 --activate
WORKDIR /app

COPY package.json pnpm-lock.yaml pnpm-workspace.yaml turbo.json ./
COPY packages/api-spec/package.json packages/api-spec/
COPY packages/typescript-config/package.json packages/typescript-config/
COPY apps/developers-page/package.json apps/developers-page/

RUN pnpm install --filter ./apps/developers-page... --filter @repo/api-spec... --frozen-lockfile

COPY packages/api-spec packages/api-spec
COPY packages/typescript-config packages/typescript-config
COPY apps/developers-page apps/developers-page

# Compile TypeSpec → OpenAPI YAML for Scalar
RUN pnpm --filter @repo/api-spec build

ARG NEXT_BASE_PATH=/docs
ENV NEXT_BASE_PATH=$NEXT_BASE_PATH
ENV NEXT_TELEMETRY_DISABLED=1

RUN pnpm --filter ./apps/developers-page build

FROM node:22-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
ENV PORT=3000
ENV HOSTNAME=0.0.0.0
ENV OPENAPI_SPEC_ROOT=/app/openapi-specs

RUN addgroup --system --gid 1001 nodejs \
  && adduser --system --uid 1001 nextjs

COPY --from=build --chown=nextjs:nodejs /app/apps/developers-page/.next/standalone ./
COPY --from=build --chown=nextjs:nodejs /app/apps/developers-page/.next/static ./apps/developers-page/.next/static
COPY --from=build --chown=nextjs:nodejs /app/apps/developers-page/public ./apps/developers-page/public
# Specs next to process (routes use OPENAPI_SPEC_ROOT)
COPY --from=build --chown=nextjs:nodejs /app/packages/api-spec/dist ./openapi-specs

USER nextjs
EXPOSE 3000
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD wget -qO- http://127.0.0.1:3000/docs || exit 1

CMD ["node", "apps/developers-page/server.js"]
