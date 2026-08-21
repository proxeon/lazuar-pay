const oneApi =
  import.meta.env.VITE_ONE_API_URL ?? 'http://localhost:8080/api/v1'

export async function createTenant(
  accessToken: string,
  name: string,
  slug: string,
): Promise<{ id: string; slug: string; name: string }> {
  const response = await fetch(`${oneApi.replace(/\/$/, '')}/tenants`, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${accessToken}`,
      'Content-Type': 'application/json',
      Accept: 'application/json',
    },
    body: JSON.stringify({ name, slug }),
  })
  if (!response.ok) {
    throw new Error(`create tenant ${response.status}`)
  }
  return (await response.json()) as { id: string; slug: string; name: string }
}
