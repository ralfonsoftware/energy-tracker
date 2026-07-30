import i18n from '@/lib/i18n'

const BASE = '/api/v1'

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const isFormData = init?.body instanceof FormData
  let res: Response
  try {
    res = await fetch(`${BASE}${path}`, {
      ...init,
      headers: isFormData
        ? init?.headers
        : { 'Content-Type': 'application/json', ...init?.headers },
    })
  } catch (cause) {
    const message = i18n.t('errors.networkError', { ns: 'common' })
    const err = new Error(message, { cause })
    Object.assign(err, { detail: message })
    throw err
  }
  if (!res.ok) {
    const problem = await res.json().catch(() => ({ detail: 'Unknown error' }))
    const err = new Error(problem.detail ?? 'API error')
    Object.assign(err, problem)
    throw err
  }
  if (res.status === 204) return undefined as T
  const text = await res.text()
  return text ? (JSON.parse(text) as T) : (undefined as T)
}

export const apiClient = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, {
      method: 'POST',
      body: body !== undefined ? JSON.stringify(body) : undefined,
    }),
  patch: <T>(path: string, body: unknown) =>
    request<T>(path, { method: 'PATCH', body: JSON.stringify(body) }),
  put: <T>(path: string, body: unknown) =>
    request<T>(path, { method: 'PUT', body: JSON.stringify(body) }),
  delete: <T>(path: string, body?: unknown) =>
    request<T>(path, {
      method: 'DELETE',
      body: body !== undefined ? JSON.stringify(body) : undefined,
    }),
  postForm: <T>(path: string, formData: FormData) =>
    request<T>(path, { method: 'POST', body: formData }),
}
