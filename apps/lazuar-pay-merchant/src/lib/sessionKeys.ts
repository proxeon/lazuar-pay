/** sessionStorage only — not an authz cookie. */
export const RETURN_TO_KEY = 'lazuar-pay-merchant:returnTo'
export const ORG_HINT_KEY = 'lazuar-pay-merchant:orgId'

export function isSafeReturnPath(value: string): boolean {
  return value.startsWith('/') && !value.startsWith('//')
}

export function setReturnTo(pathWithSearch: string): void {
  if (isSafeReturnPath(pathWithSearch)) {
    sessionStorage.setItem(RETURN_TO_KEY, pathWithSearch)
  }
}

export function peekReturnTo(): string | null {
  const value = sessionStorage.getItem(RETURN_TO_KEY)
  if (value && isSafeReturnPath(value)) return value
  return null
}

export function takeReturnTo(): string | null {
  const value = sessionStorage.getItem(RETURN_TO_KEY)
  if (value) sessionStorage.removeItem(RETURN_TO_KEY)
  if (value && isSafeReturnPath(value)) return value
  return null
}

/** Survives React StrictMode remount so login completion does not drop returnTo. */
let takenOnce: string | null | undefined

export function takeReturnToOnce(): string | null {
  if (takenOnce === undefined) takenOnce = takeReturnTo()
  return takenOnce
}

export function getOrgHint(): string | null {
  return sessionStorage.getItem(ORG_HINT_KEY)
}

export function setOrgHint(orgId: string): void {
  sessionStorage.setItem(ORG_HINT_KEY, orgId)
}
