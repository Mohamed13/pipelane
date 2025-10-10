import { spawn } from 'child_process';
import fs from 'fs';
import path from 'path';
import url from 'url';

const __dirname = path.dirname(url.fileURLToPath(import.meta.url));
const frontRoot = path.resolve(__dirname, '..');
const apiRoot = path.resolve(frontRoot, '..', 'pipelane-api');
const apiProj = path.join(apiRoot, 'src', 'Pipelane.Api', 'Pipelane.Api.csproj');
const swaggerOut = path.join(frontRoot, 'src', 'app', 'api', 'swagger.json');

function wait(ms) {
  return new Promise((r) => setTimeout(r, ms));
}

async function fetchWithRetries(url, retries = 30) {
  for (let i = 0; i < retries; i++) {
    try {
      const res = await fetch(url);
      if (res.ok) {
        return await res.text();
      }
    } catch {}
    await wait(500);
  }
  throw new Error('Cannot fetch swagger.json');
}

async function main() {
  console.log('Starting API to fetch Swagger...');
  const child = spawn(
    process.platform === 'win32' ? 'dotnet.exe' : 'dotnet',
    ['run', '--project', apiProj],
    {
      cwd: apiRoot,
      env: { ...process.env, ASPNETCORE_URLS: 'http://localhost:5001' },
      stdio: 'ignore',
      detached: true,
    },
  );

  try {
    const json = await fetchWithRetries('http://localhost:5001/swagger/v1/swagger.json');
    fs.mkdirSync(path.dirname(swaggerOut), { recursive: true });
    fs.writeFileSync(swaggerOut, json, 'utf-8');
    console.log('Saved', path.relative(frontRoot, swaggerOut));
  } finally {
    try {
      process.kill(-child.pid);
    } catch {
      try {
        process.kill(child.pid);
      } catch {}
    }
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
