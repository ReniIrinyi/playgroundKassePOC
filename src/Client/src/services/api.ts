const BASE = (import.meta.env.VITE_API_BASE_URL || 'http://localhost:5005')
  .replace(/\/+$/, '');

async function post(path: string, body?: any) {
  const res = await fetch(`${BASE}${path}`, {
    method: 'POST',
    headers: {'Content-Type': 'application/json'},
    body: body ? JSON.stringify(body) : undefined
  });
  if (!res.ok) throw new Error(await res.text());
  return res.json();
}

export const deviceApi = {
  ping: () => fetch(`${BASE}/api/ping`).then(r => r.json()),
  openDrawer: () => post('/api/drawer/open'),
  print: (p: { text: string, cut?: boolean, openDrawerAfter?: boolean }) =>
    post('/api/printer/print', p),
};
