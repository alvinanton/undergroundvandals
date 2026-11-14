// Comprobación estática de cobertura. No realiza peticiones ni cambia archivos.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.dirname(fileURLToPath(import.meta.url));
const repo = path.dirname(root);
const errors = [];
const check = (condition, message) => { if (!condition) errors.push(message); };
const read = p => fs.readFileSync(p, 'utf8');
const walk = p => fs.readdirSync(p, { withFileTypes: true }).flatMap(e =>
  e.isDirectory() ? walk(path.join(p, e.name)) : [path.join(p, e.name)]);
const block = (s, name) => s.match(new RegExp(`^${name} \\{\\r?\\n([\\s\\S]*?)^\\}`, 'm'))?.[1];
const canonical = s => s.toLowerCase().replace(/\{\{\w+\}\}/g, ':param').replace(/\{[^}]+\}/g, ':param');
const config = JSON.parse(read(path.join(root, 'bruno.json')));
check(config.type === 'collection' && config.version === '1', 'bruno.json inválido');
const collection = read(path.join(root, 'collection.bru'));
check(block(collection, 'auth')?.includes('mode: bearer'), 'Falta Bearer en colección');
check(block(collection, 'auth:bearer')?.includes('token: {{token}}'), 'Falta variable token');

const environmentFiles = fs.readdirSync(path.join(root, 'environments')).filter(f => f.endsWith('.bru'));
const environments = environmentFiles.map(f => {
  const source = read(path.join(root, 'environments', f));
  const names = [...(block(source, 'vars') || '').matchAll(/^\s+(\w+):/gm)].map(m => m[1]);
  const secrets = source.match(/vars:secret \[([\s\S]*?)\]/)?.[1].split(/[\s,]+/).filter(Boolean) || [];
  check(names.includes('baseUrl'), `${f}: falta baseUrl`);
  check(!/baseUrl: .*\/$/.test(source), `${f}: baseUrl con slash final`);
  check(['password', 'token', 'newPassword'].every(k => secrets.includes(k)), `${f}: faltan secretos`);
  check(!names.some(k => ['password', 'token', 'newPassword'].includes(k)), `${f}: secreto en vars normales`);
  return { name: f, names: new Set([...names, ...secrets]) };
});
check(environmentFiles.length >= 3, 'Faltan entornos');

const requests = walk(root).filter(p => p.endsWith('.bru') && /type: http/.test(read(p))).map(p => {
  const source = read(p);
  const method = source.match(/^(get|post|put|patch|delete) \{/m)?.[1];
  const operation = method && block(source, method);
  const url = operation?.match(/^\s+url: (.+)$/m)?.[1].trim();
  check(Boolean(method && url), `${p}: operación inválida`);
  check(url?.startsWith('{{baseUrl}}/api/'), `${p}: URL sin baseUrl`);
  check(Boolean(block(source, 'docs')), `${p}: sin documentación`);
  check(/res.status: eq \d{3}/.test(block(source, 'assert') || ''), `${p}: sin aserción`);
  const body = block(source, 'body:json');
  if (body) {
    try { JSON.parse(body); } catch (e) { errors.push(`${p}: body JSON inválido: ${e.message}`); }
    check(/Content-Type: application\/json/.test(block(source, 'headers') || ''), `${p}: falta Content-Type`);
  }
  const script = block(source, 'script:post-response');
  if (script) {
    try { new Function('bru', 'res', script); } catch (e) { errors.push(`${p}: script inválido: ${e.message}`); }
  }
  const refs = [...source.matchAll(/\{\{(\w+)\}\}/g)].map(m => m[1]);
  for (const env of environments) for (const key of refs) {
    check(env.names.has(key), `${p}: ${key} no declarada en ${env.name}`);
  }
  const query = block(source, 'params:query');
  if (query) {
    const enabled = [...query.matchAll(/^\s+(\w+): (.+)$/gm)].map(m => `${m[1]}=${m[2].trim()}`);
    check(url?.split('?')[1] === enabled.join('&'), `${p}: query y URL no coinciden`);
  }
  const folder = path.basename(path.dirname(p));
  return { p, method, url, source, folder,
    key: `${method?.toUpperCase()} ${canonical((url || '').replace('{{baseUrl}}', '').split('?')[0])}`,
    auth: operation?.match(/^\s+auth: (\w+)/m)?.[1],
    seq: block(source, 'meta')?.match(/seq: (\d+)/)?.[1] };
});

for (const folder of new Set(requests.map(r => r.folder))) {
  const group = requests.filter(r => r.folder === folder);
  check(new Set(group.map(r => r.seq)).size === group.length && group.every(r => r.seq), `${folder}: secuencias inválidas`);
  check(fs.existsSync(path.join(root, folder, 'folder.bru')), `${folder}: falta folder.bru`);
}

const expectedBackend = [];
const controllers = path.join(repo, 'backend/UndergroundVandals/UndergroundVandals.Api/Controllers');
for (const p of walk(controllers).filter(p => p.endsWith('.cs'))) {
  const source = read(p);
  const controller = source.match(/class (\w+)Controller/)?.[1];
  if (!controller) continue;
  const classAuth = /\[Authorize\b/.test(source.slice(0, source.indexOf(`class ${controller}Controller`)));
  const action = /((?:\s*\[[^\]]+\])+)[\s\r\n]*public\s+async\s+Task[^\n]+/g;
  for (const m of source.matchAll(action)) {
    const http = m[1].match(/\[Http(Get|Post|Put|Patch|Delete)(?:\("([^"]*)"\))?\]/);
    if (!http) continue;
    expectedBackend.push({ key: `${http[1].toUpperCase()} ${canonical(`/api/${controller}${http[2] ? '/' + http[2] : ''}`)}`,
      auth: classAuth || /\[Authorize\b/.test(m[1]) ? 'inherit' : 'none' });
  }
}
const expectedAstro = [];
const astroRoot = path.join(repo, 'frontend/src/pages/api');
for (const p of walk(astroRoot).filter(p => p.endsWith('.ts'))) {
  const route = '/api/' + path.relative(astroRoot, p).replaceAll('\\', '/').replace(/(?:\/index)?\.ts$/, '').replace(/\[[^\]]+\]/g, ':param');
  for (const m of read(p).matchAll(/export const (GET|POST|PUT|PATCH|DELETE):/g)) {
    expectedAstro.push({ key: `${m[1]} ${route.toLowerCase()}` });
  }
}
for (const [expected, actual, label] of [
  [expectedBackend, requests.filter(r => !r.folder.startsWith('06')), '.NET'],
  [expectedAstro, requests.filter(r => r.folder.startsWith('06')), 'Astro'],
]) {
  for (const e of expected) {
    const matches = actual.filter(r => r.key === e.key);
    check(matches.length === 1, `${label}: ${e.key} requiere exactamente un request, tiene ${matches.length}`);
    if (e.auth && matches[0]) check(matches[0].auth === e.auth, `${label}: auth incorrecta en ${e.key}`);
  }
  for (const r of actual) check(expected.some(e => e.key === r.key), `${label}: request sin endpoint ${r.key}`);
}
if (errors.length) {
  console.error(errors.join('\n'));
  process.exitCode = 1;
} else {
  console.log(`OK: ${expectedBackend.length} operaciones .NET y ${expectedAstro.length} Astro; ${requests.length} requests, ${environments.length} entornos, JSON, scripts, variables y permisos verificados.`);
}
