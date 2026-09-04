import { WebStorageStateStore, type UserManagerSettings } from 'oidc-client-ts'
import type { AuthProviderProps } from 'react-oidc-context'

/**
 * Shared UserManager settings. Silent-renew must use this object, not
 * AuthProviderProps (that type is a union and has no `.authority`).
 */
export function getOidcSettings(): UserManagerSettings {
  const authority =
    import.meta.env.VITE_ZITADEL_AUTHORITY || 'http://localhost:8085'
  const client_id = import.meta.env.VITE_ZITADEL_CLIENT_ID || ''
  const redirect_uri =
    import.meta.env.VITE_ZITADEL_REDIRECT_URI ||
    'http://localhost:5178/callback'
  const silent_redirect_uri = `${new URL(redirect_uri).origin}/silent-renew.html`
  const post_logout_redirect_uri =
    import.meta.env.VITE_ZITADEL_POST_LOGOUT_REDIRECT_URI ||
    'http://localhost:5178/'
  const scope =
    import.meta.env.VITE_ZITADEL_SCOPE ||
    'openid profile email offline_access'

  return {
    authority,
    client_id,
    redirect_uri,
    silent_redirect_uri,
    post_logout_redirect_uri,
    scope,
    response_type: 'code',
    userStore: new WebStorageStateStore({ store: window.sessionStorage }),
  }
}

/**
 * Public SPA: authorization code + PKCE.
 * Login UI is One :5175 (issuer is Zitadel :8085). Homepage is :5178.
 * Tokens in sessionStorage — not cookies (localhost cookies are not port-scoped).
 */
export function getOidcConfig(): AuthProviderProps {
  return {
    ...getOidcSettings(),
    automaticSilentRenew: true,
    onSigninCallback: () => {
      window.history.replaceState({}, document.title, window.location.pathname)
    },
  }
}
