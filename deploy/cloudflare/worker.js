// Cloudflare Worker: serves the static Arabic UI and proxies /api/* + /health to the .NET API (API_ORIGIN).
// Cloudflare cannot run .NET/SQL Server; the API must be hosted elsewhere (see HANDOFF.md §8).
export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (url.pathname.startsWith('/api/') || url.pathname === '/health') {
      const target = env.API_ORIGIN + url.pathname + url.search;
      const init = { method: request.method, headers: request.headers, redirect: 'manual' };
      if (!['GET', 'HEAD'].includes(request.method)) init.body = await request.arrayBuffer();
      return fetch(target, init);
    }
    return env.ASSETS.fetch(request);
  }
};
