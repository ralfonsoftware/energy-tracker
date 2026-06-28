export interface SwaAuthUser {
  identityProvider: string;
  userId: string;
  userDetails: string;
  userRoles: string[];
  claims?: Array<{ typ: string; val: string }>;
}

interface MeResponse {
  clientPrincipal: SwaAuthUser | null;
}

export async function getMe(): Promise<SwaAuthUser | null> {
  try {
    const res = await fetch('/.auth/me');
    if (!res.ok) return null;
    const data: MeResponse = await res.json();
    return data.clientPrincipal ?? null;
  } catch {
    return null;
  }
}

export function login(returnUrl: string = window.location.href): void {
  window.location.href = `/.auth/login/aad?post_login_redirect_uri=${encodeURIComponent(returnUrl)}`;
}

export function logout(returnUrl: string = '/'): void {
  window.location.href = `/.auth/logout?post_logout_redirect_uri=${encodeURIComponent(returnUrl)}`;
}
