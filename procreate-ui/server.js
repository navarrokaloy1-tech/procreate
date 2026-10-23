// Serves the built Angular app, behind an optional HTTP Basic Auth gate for UAT.
//
// The gate is ON only when both UAT_USER and UAT_PASSWORD are set. To remove it
// after UAT, delete those two env vars in Render — the site then serves with no
// prompt, no code change needed. Zero dependencies: Node built-ins only.
const http = require('http');
const fs = require('fs');
const path = require('path');

const PORT = process.env.PORT || 10000;
const ROOT = path.join(__dirname, 'dist', 'procreate-ui', 'browser');

const USER = process.env.UAT_USER;
const PASS = process.env.UAT_PASSWORD;
const AUTH_ON = Boolean(USER && PASS);

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript',
  '.mjs': 'text/javascript',
  '.css': 'text/css',
  '.json': 'application/json',
  '.ico': 'image/x-icon',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.gif': 'image/gif',
  '.svg': 'image/svg+xml',
  '.webp': 'image/webp',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2',
  '.ttf': 'font/ttf',
  '.map': 'application/json',
  '.txt': 'text/plain; charset=utf-8',
  '.webmanifest': 'application/manifest+json',
};

function authorized(req) {
  if (!AUTH_ON) return true;
  const header = req.headers.authorization || '';
  if (!header.startsWith('Basic ')) return false;
  const decoded = Buffer.from(header.slice(6), 'base64').toString('utf8');
  const sep = decoded.indexOf(':');
  const u = decoded.slice(0, sep);
  const p = decoded.slice(sep + 1);
  return u === USER && p === PASS;
}

function sendFile(res, filePath, status = 200) {
  fs.readFile(filePath, (err, data) => {
    if (err) {
      res.writeHead(404);
      res.end('Not found');
      return;
    }
    const ext = path.extname(filePath).toLowerCase();
    res.writeHead(status, { 'Content-Type': MIME[ext] || 'application/octet-stream' });
    res.end(data);
  });
}

const indexHtml = path.join(ROOT, 'index.html');

const server = http.createServer((req, res) => {
  if (!authorized(req)) {
    res.writeHead(401, { 'WWW-Authenticate': 'Basic realm="Pro-Create UAT"' });
    res.end('Authentication required.');
    return;
  }

  const urlPath = decodeURIComponent((req.url || '/').split('?')[0]);
  let filePath = path.normalize(path.join(ROOT, urlPath));

  // Never serve outside the build directory.
  if (!filePath.startsWith(ROOT)) {
    res.writeHead(403);
    res.end('Forbidden');
    return;
  }

  fs.stat(filePath, (err, stat) => {
    if (!err && stat.isDirectory()) filePath = path.join(filePath, 'index.html');

    fs.access(filePath, fs.constants.R_OK, (missing) => {
      // Unknown path with no file extension → SPA route, serve index.html.
      if (missing) {
        sendFile(res, indexHtml);
        return;
      }
      sendFile(res, filePath);
    });
  });
});

server.listen(PORT, () => {
  console.log(`Pro-Create UI on :${PORT} — UAT gate ${AUTH_ON ? 'ON' : 'OFF'}`);
});
