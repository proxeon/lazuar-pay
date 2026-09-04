# syntax=docker/dockerfile:1

FROM node:22-alpine AS build
RUN corepack enable && corepack prepare pnpm@11.5.2 --activate
WORKDIR /app

COPY package.json pnpm-lock.yaml pnpm-workspace.yaml turbo.json ./
COPY apps/lazuar-pay-checkout/package.json apps/lazuar-pay-checkout/
RUN pnpm install --filter ./apps/lazuar-pay-checkout... --frozen-lockfile

COPY apps/lazuar-pay-checkout apps/lazuar-pay-checkout

ARG VITE_PAY_API_URL
ENV VITE_PAY_API_URL=$VITE_PAY_API_URL
RUN test -n "$VITE_PAY_API_URL"
RUN pnpm --filter ./apps/lazuar-pay-checkout build

FROM node:22-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production
RUN npm install -g serve@14.2.4 \
  && addgroup -g 1001 web \
  && adduser -D -u 1001 -G web web
COPY --from=build --chown=web:web /app/apps/lazuar-pay-checkout/dist ./dist
COPY --chown=web:web apps/lazuar-pay-checkout/serve.json ./serve.json
USER web
EXPOSE 5179
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget -qO- http://127.0.0.1:5179/ || exit 1
CMD ["serve", "-s", "dist", "-l", "5179"]
