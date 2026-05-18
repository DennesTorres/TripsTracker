import { LogLevel } from '@azure/msal-browser';
import type { Configuration } from '@azure/msal-browser';

// ── Microsoft identity platform configuration ─────────────────────────────────
// Uses the /common endpoint — supports personal Microsoft accounts.
// Set these environment variables in .env.production.local for local dev
// and in GitHub secrets / Azure SWA config for deployed environments.
//
//   VITE_AUTH_CLIENT_ID   — App registration Client ID (GUID)

const clientId = import.meta.env.VITE_AUTH_CLIENT_ID ?? 'REPLACE_CLIENT_ID';

// api://{clientId}/access_as_user — the specific scope exposed by this application.
// Requests only the permissions declared under "Expose an API" for this scope.
export const apiScope = `api://${clientId}/access_as_user`;

export const msalConfig: Configuration = {
  auth: {
    clientId,
    authority: 'https://login.microsoftonline.com/common/v2.0',
    knownAuthorities: ['login.microsoftonline.com'],
    redirectUri: window.location.origin,
    postLogoutRedirectUri: window.location.origin,
  },
  cache: {
    cacheLocation: 'sessionStorage',
  },
  system: {
    loggerOptions: {
      loggerCallback: (level, message, containsPii) => {
        if (containsPii) return;
        if (level <= LogLevel.Warning) console.warn(`[MSAL] ${message}`);
      },
      logLevel: LogLevel.Warning,
    },
  },
};

export const loginRequest = {
  // access_as_user — the specific scope exposed by this app; audience = api://{clientId}.
  // email ensures the email claim is present for user identification on the backend.
  scopes: [apiScope, 'email'],
};
