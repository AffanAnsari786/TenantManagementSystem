import { InjectionToken } from '@angular/core';

/** Base URL for the REST API (e.g. http://localhost:5149/api). */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL');

/** Base URL for SignalR hubs (e.g. http://localhost:5149/hubs). */
export const HUB_BASE_URL = new InjectionToken<string>('HUB_BASE_URL');
