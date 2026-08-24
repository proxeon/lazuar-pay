export async function problemDetail(response: Response, fallback: string): Promise<string> {
  try {
    const body = (await response.json()) as { detail?: string }
    if (body.detail && body.detail.trim()) return body.detail
  } catch {
    /* ignore */
  }
  return fallback
}
