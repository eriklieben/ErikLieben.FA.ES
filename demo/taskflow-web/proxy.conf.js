const ASPIRE_API_URL = process.env.services__api__http__0 || process.env.services__api__https__0;
const ASPIRE_FUNCTIONS_URL = process.env.services__functions__http__0
    || process.env.services__functions__https__0
    || process.env.services__functions__default__0;

const apiTarget = ASPIRE_API_URL || 'https://taskflow-api.dev.localhost';
const functionsTarget = ASPIRE_FUNCTIONS_URL || 'http://localhost:7071';

console.log('='.repeat(60));
console.log('PROXY CONFIGURATION DEBUG');
console.log('='.repeat(60));
console.log('Environment variables:');
console.log('  services__api__http__0:', process.env.services__api__http__0);
console.log('  services__api__https__0:', process.env.services__api__https__0);
console.log('  services__functions__http__0:', process.env.services__functions__http__0);
console.log('  services__functions__https__0:', process.env.services__functions__https__0);
console.log('  services__functions__default__0:', process.env.services__functions__default__0);
console.log('All env vars containing "functions":');
Object.keys(process.env).filter(k => k.toLowerCase().includes('functions')).forEach(k => {
  console.log(`  ${k}: ${process.env[k]}`);
});
console.log('Resolved targets:');
console.log('  API target:', apiTarget);
console.log('  Functions target:', functionsTarget);
console.log('='.repeat(60));

module.exports = {
  '/api': {
    target: apiTarget,
    secure: false,
    changeOrigin: true,
    logLevel: 'debug'
  },
  '/hub': {
    target: apiTarget,
    secure: false,
    changeOrigin: true,
    ws: true,
    logLevel: 'debug'
  },
  '/functions-api': {
    target: functionsTarget,
    secure: false,
    changeOrigin: true,
    pathRewrite: {
      '^/functions-api': '/api'
    },
    logLevel: 'debug',
    onProxyReq: (proxyReq, req, res) => {
      console.log(`[functions-api] Proxying ${req.method} ${req.url} -> ${functionsTarget}${proxyReq.path}`);
    },
    onError: (err, req, res) => {
      console.error('[functions-api] Proxy error:', err.message);
    }
  }
};
