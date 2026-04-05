/**
 * Default (development) environment.
 * For production builds, replace via angular.json fileReplacements with
 * environment.prod.ts — or read from a runtime config endpoint.
 */
export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:5149/api',
  hubBaseUrl: 'http://localhost:5149/hubs'
};
