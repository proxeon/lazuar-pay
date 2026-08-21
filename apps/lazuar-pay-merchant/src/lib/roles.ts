/** One tenant roles. Pay: owner/admin write money; member is read-only. */
export function canWriteMoney(role: string | undefined | null): boolean {
  return role === 'owner' || role === 'admin'
}
