import { describe, expect, it } from 'vitest'
import type { User } from 'oidc-client-ts'
import { isJwtLike, pickApiBearerToken } from './bearerToken'

const JWT = 'eyJhbGciOiJub25lIn0.eyJzdWIiOiIxIn0.sig'
const JWT_ID = 'eyJhbGciOiJub25lIn0.eyJzdWIiOiJpZCJ9.sig'
const OPAQUE = 'ZrP3opaqueAccessTokenWithoutDots'
const JWE = 'a.b.c.d.e'

function user(access?: string, id?: string): User {
  return { access_token: access ?? '', id_token: id } as User
}

describe('isJwtLike', () => {
  it('accepts compact JWS and rejects opaque / JWE / empty', () => {
    expect(isJwtLike(JWT)).toBe(true)
    expect(isJwtLike(OPAQUE)).toBe(false)
    expect(isJwtLike(JWE)).toBe(false)
    expect(isJwtLike('')).toBe(false)
  })
})

describe('pickApiBearerToken', () => {
  it('returns undefined when signed out', () => {
    expect(pickApiBearerToken(null)).toBeUndefined()
    expect(pickApiBearerToken(undefined)).toBeUndefined()
  })

  it('sends JWT access_token and never the companion id_token', () => {
    expect(pickApiBearerToken(user(JWT, JWT_ID))).toBe(JWT)
    expect(pickApiBearerToken(user(JWT, JWT_ID))).not.toBe(JWT_ID)
  })

  it('does not fall back to JWT id_token when access is opaque or empty', () => {
    expect(pickApiBearerToken(user(OPAQUE, JWT_ID))).toBeUndefined()
    expect(pickApiBearerToken(user('', JWT_ID))).toBeUndefined()
    expect(pickApiBearerToken(user(JWE, JWT_ID))).toBeUndefined()
  })
})
