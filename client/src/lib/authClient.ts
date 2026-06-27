export interface ClientPrincipal {
  userId: string
  userDetails: string
  identityProvider: string
  userRoles: string[]
}

export interface MeResponse {
  clientPrincipal: ClientPrincipal | null
}

export const authClient = {
  login: () => {
    window.location.href = '/.auth/login/aad'
  },
  logout: () => {
    window.location.href = '/.auth/logout'
  },
  getMe: (): Promise<MeResponse> =>
    fetch('/.auth/me').then((r) => r.ok ? r.json() : Promise.resolve({ clientPrincipal: null })),
}
