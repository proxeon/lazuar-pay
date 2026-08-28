# syntax=docker/dockerfile:1

FROM node:22-alpine AS build
RUN corepack enable && corepack prepare pnpm@11.5.2 --activate
WORKDIR /app

COPY package.json pnpm-lock.yaml pnpm-workspace.yaml turbo.json ./
COPY apps/lazuar-pay-merchant/package.json apps/lazuar-pay-merchant/
RUN pnpm install --filter ./apps/lazuar-pay-merchant... --frozen-lockfile

COPY apps/lazuar-pay-merchant apps/lazuar-pay-merchant

ARG VITE_PAY_API_URL
ARG VITE_CHECKOUT_ORIGIN
ARG VITE_ONE_API_URL
ARG VITE_ZITADEL_AUTHORITY
ARG VITE_ZITADEL_CLIENT_ID
ARG VITE_ZITADEL_REDIRECT_URI
ARG VITE_ZITADEL_POST_LOGOUT_REDIRECT_URI
ARG VITE_ZITADEL_SCOPE="openid profile email offline_access"
ENV VITE_PAY_API_URL=$VITE_PAY_API_URL
ENV VITE_CHECKOUT_ORIGIN=$VITE_CHECKOUT_ORIGIN
ENV VITE_ONE_API_URL=$VITE_ONE_API_URL
ENV VITE_ZITADEL_AUTHORITY=$VITE_ZITADEL_AUTHORITY
ENV VITE_ZITADEL_CLIENT_ID=$VITE_ZITADEL_CLIENT_ID
ENV VITE_ZITADEL_REDIRECT_URI=$VITE_ZITADEL_REDIRECT_URI
ENV VITE_ZITADEL_POST_LOGOUT_REDIRECT_URI=$VITE_ZITADEL_POST_LOGOUT_REDIRECT_URI
ENV VITE_ZITADEL_SCOPE=$VITE_ZITADEL_SCOPE

RUN test -n "$VITE_PAY_API_URL" && test -n "$VITE_CHECKOUT_ORIGIN"
RUN pnpm --filter ./apps/lazuar-pay-merchant build

FROM node:22-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production
RUN npm install -g serve@14.2.4 \
  && addgroup -g 1001 web \
  && adduser -D -u 1001 -G web web
COPY --from=build --chown=web:web /app/apps/lazuar-pay-merchant/dist ./dist
USER web
EXPOSE 5178
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget -qO- http://127.0.0.1:5178/ || exit 1
CMD ["serve", "-s", "dist", "-l", "5178"]
