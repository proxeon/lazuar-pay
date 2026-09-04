import { UserManager, WebStorageStateStore } from 'oidc-client-ts'
import { getOidcConfig } from './oidcConfig'

/** Iframe target for automaticSilentRenew. Must not load CallbackPage. */
const cfg = getOidcConfig()
const manager = new UserManager({
  authority: cfg.authority,
  client_id: cfg.client_id,
  redirect_uri: cfg.redirect_uri,
  silent_redirect_uri: cfg.silent_redirect_uri,
  post_logout_redirect_uri: cfg.post_logout_redirect_uri,
  scope: cfg.scope,
  response_type: cfg.response_type ?? 'code',
  userStore: cfg.userStore ?? new WebStorageStateStore({ store: window.sessionStorage }),
})

void manager.signinSilentCallback()
