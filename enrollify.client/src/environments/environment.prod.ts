export const environment = {
  production: true,
  // Relative on purpose: production serves the SPA and the API behind one
  // reverse proxy, so /api routes to the backend on the same origin.
  apiUrl: '/api',
  defaultTenantId: ''
};
