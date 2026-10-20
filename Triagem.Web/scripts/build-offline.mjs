import { readdir, readFile, writeFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
async function list(path, prefix = '') {
  const entries = await readdir(path, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const name = `${prefix}/${entry.name}`;
    if (entry.isDirectory()) files.push(...await list(`${path}/${entry.name}`, name));
    else if (entry.name !== 'sw.js') files.push(name);
  }
  return files;
}
const files = await list('dist');
const contents = await Promise.all(files.map(file => readFile(`dist${file}`)));
const hash = createHash('sha256'); contents.forEach(content => hash.update(content));
const cache = `triar-shell-${hash.digest('hex').slice(0, 16)}`;
await writeFile('dist/sw.js', `
const CACHE=${JSON.stringify(cache)};
const FILES=${JSON.stringify(files)};
self.addEventListener('install', event => { event.waitUntil(caches.open(CACHE).then(cache => cache.addAll(FILES))); });
self.addEventListener('activate', event => { event.waitUntil(caches.keys().then(keys => Promise.all(keys.filter(key => key.startsWith('triar-shell-') && key !== CACHE).map(key => caches.delete(key)))).then(() => self.clients.claim())); });
self.addEventListener('fetch', event => {
  const url = new URL(event.request.url);
  if (event.request.method !== 'GET' || url.origin !== self.location.origin || url.pathname.startsWith('/api/') || url.pathname === '/health') return;
  if (event.request.mode === 'navigate') { event.respondWith(caches.open(CACHE).then(cache => cache.match('/index.html')).then(cached => cached || fetch(event.request))); return; }
  if (FILES.includes(url.pathname)) event.respondWith(caches.match(event.request).then(cached => cached || fetch(event.request)));
});
`);
