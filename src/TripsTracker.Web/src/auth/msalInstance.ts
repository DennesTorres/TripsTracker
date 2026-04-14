import { PublicClientApplication, EventType } from '@azure/msal-browser';
import type { AuthenticationResult, EventMessage } from '@azure/msal-browser';
import { msalConfig } from './authConfig';

/**
 * Singleton MSAL instance shared across the application.
 *
 * Only initialize() is called here — handleRedirectPromise() is called
 * internally by MsalProvider when it mounts. Calling it here would consume
 * the redirect response before MsalProvider can process it into React state.
 */
export const msalInstance = new PublicClientApplication(msalConfig);

export const msalInitialized = msalInstance.initialize().then(() => {
  // Restore active account from cache on normal page load (browser refresh)
  const accounts = msalInstance.getAllAccounts();
  if (accounts.length > 0) {
    msalInstance.setActiveAccount(accounts[0]);
  }

  // Set active account when login redirect completes.
  // MsalProvider calls handleRedirectPromise() internally, which emits
  // LOGIN_SUCCESS on success. This callback picks up the account.
  msalInstance.addEventCallback((event: EventMessage) => {
    if (event.eventType === EventType.LOGIN_SUCCESS && event.payload) {
      const result = event.payload as AuthenticationResult;
      msalInstance.setActiveAccount(result.account);
    }
  });
});
