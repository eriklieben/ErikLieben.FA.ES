# taskflow-web — local HTTPS

The Angular dev server is configured with `ssl: true` in `angular.json`, so
`ng serve` auto-generates a self-signed certificate on every run. **No setup
is required for normal development** — your browser will warn about the
self-signed cert; accept it once per session.

## When you want a stable cert

Use a persistent cert if you need:

- HSTS to stick across restarts
- A cert that's valid for extra hostnames (e.g. `host.docker.internal`,
  `*.dev.localhost`) so the API container and the Angular dev server share trust
- The cert to survive `ng serve` restarts during long debugging sessions

Generate one with the .NET dev-certs tool:

```bash
# From this directory (demo/taskflow-web/):
dotnet dev-certs https --trust
dotnet dev-certs https \
    --export-path ./aspnetapp.pem \
    --no-password \
    --format Pem
```

This writes `aspnetapp.pem` (certificate) and `aspnetapp.key` (private key)
next to `angular.json`. Both files are `.gitignore`-d at the repo root.

Then wire them into `angular.json` under `serve.options`:

```jsonc
"serve": {
  "builder": "@angular/build:dev-server",
  "options": {
    "ssl": true,
    "sslCert": "./aspnetapp.pem",
    "sslKey":  "./aspnetapp.key",
    "proxyConfig": "proxy.conf.js"
  },
  ...
}
```

## Notes

- **Never commit `*.pem`, `*.key`, `*.pfx`, `*.p12`, or `*.crt`.** They are
  ignored at the repo root.
- On Linux, `--trust` only updates the user-level NSS trust store; Chromium
  reads it, Firefox does not. Add the cert to Firefox manually if needed.
- To wipe and regenerate: `dotnet dev-certs https --clean` followed by the
  export command again.
