import { UserManager, WebStorageStateStore } from 'oidc-client-ts'

/** Iframe target for automaticSilentRenew. Must not load CallbackPage. */
const redirect_uri =
  import.meta.env.VITE_ZITADEL_REDIRECT_URI || 'http://localhost:5178/callback'
const manager = new UserManager({
  authority: import.meta.env.VITE_ZITADEL_AUTHORITY || 'http://localhost:8085',
  client_id: import.meta.env.VITE_ZITADEL_CLIENT_ID || '',
  redirect_uri,
  silent_redirect_uri: `${new URL(redirect_uri).origin}/silent-renew.html`,
  userStore: new WebStorageStateStore({ store: window.sessionStorage }),
})

void manager.signinSilentCallback()
