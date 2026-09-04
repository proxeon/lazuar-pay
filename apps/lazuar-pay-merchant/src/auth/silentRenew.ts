import { UserManager } from 'oidc-client-ts'
import { getOidcSettings } from './oidcConfig'

/** Iframe target for automaticSilentRenew. Must not load CallbackPage. */
const manager = new UserManager(getOidcSettings())

void manager.signinSilentCallback()
