const BASE = (import.meta.env.VITE_API_BASE_URL || 'http://localhost:5005').replace(/\/+$/, '');
const CLOUD_BASE = (import.meta.env.VITE_CLOUD_API_URL || 'http://localhost:4200').replace(/\/+$/, '')

async function request<T>(base: string, path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${base}${path}`, {
    headers: { 'Content-Type': 'application/json', ...(init?.headers || {}) },
    ...init
  })
  if (!res.ok) {
    let msg = `HTTP ${res.status}`
    try { msg = (await res.text()) || msg } catch {}
    throw new Error(msg)
  }
  if (res.status === 204) return undefined as unknown as T
  return res.json() as Promise<T>
}

const get  = <T>(base: string, path: string) => request<T>(base, path)
const post = <T>(base: string, path: string, body?: any) =>
  request<T>(base, path, { method: 'POST', body: body ? JSON.stringify(body) : undefined })
const put  = <T>(base: string, path: string, body?: any) =>
  request<T>(base, path, { method: 'PUT',  body: body ? JSON.stringify(body) : undefined })

export type LoginDto = { user: string; pass: string; licence: string }

export const api = {
  ping:       () => get<{ ok: boolean; at: string }>(DEVICE_BASE, '/api/ping'),
  openDrawer: () => post<{ opened: boolean }>(DEVICE_BASE, '/api/drawer/open'),
  print: (p: { text: string; cut?: boolean; openDrawerAfter?: boolean }) =>
    post<{ printed: boolean }>(DEVICE_BASE, '/api/printer/print', p),
  getDeviceId: () => get<{ deviceId: string }>(APP_BASE, '/api/device-id'),
  login:       (dto: LoginDto) => post<any>(APP_BASE, '/api/login', dto),
  authStatus:  () => get<any>(APP_BASE, '/api/auth/status'),
};

export async function loginKasse(dto: LoginDto) {
  const dev = await api.getDeviceId()
  const res = await api.login(dto)
  const sess = res?.sessData ?? res?.raw ?? res
  localStorage.setItem('gic.session', JSON.stringify(sess))
  return { deviceId: dev.deviceId, session: sess }
}

