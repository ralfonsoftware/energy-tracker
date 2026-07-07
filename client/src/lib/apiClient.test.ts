import { vi, describe, it, expect, beforeEach, afterEach } from 'vitest'
import { apiClient } from '@/lib/apiClient'

describe('apiClient', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('apiClient_PostForm_SendsFormDataWithoutJsonContentTypeHeader', async () => {
    const mockFetch = vi.mocked(fetch)
    mockFetch.mockResolvedValue(
      new Response(JSON.stringify({ importJobId: 'job-1' }), { status: 202 })
    )

    const formData = new FormData()
    formData.append('file', new File(['x'], 'test.csv'))
    formData.append('plugId', 'plug-1')

    await apiClient.postForm('/flats/flat-1/imports', formData)

    expect(mockFetch).toHaveBeenCalledTimes(1)
    const [, init] = mockFetch.mock.calls[0]
    expect(init?.body).toBe(formData)
    expect(init?.headers).toBeUndefined()
  })

  it('apiClient_Post_StillSendsJsonContentTypeHeader', async () => {
    const mockFetch = vi.mocked(fetch)
    mockFetch.mockResolvedValue(new Response(JSON.stringify({ ok: true }), { status: 200 }))

    await apiClient.post('/flats/flat-1/tariffs', { pricePerKwh: 0.28 })

    const [, init] = mockFetch.mock.calls[0]
    expect(init?.headers).toEqual({ 'Content-Type': 'application/json' })
    expect(init?.body).toBe(JSON.stringify({ pricePerKwh: 0.28 }))
  })
})
