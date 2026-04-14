import axios from 'axios';
import { InteractionRequiredAuthError } from '@azure/msal-browser';
import { msalInstance, msalInitialized } from '../auth/msalInstance';
import { loginRequest } from '../auth/authConfig';

const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
});

apiClient.interceptors.request.use(async config => {
  await msalInitialized;

  const account = msalInstance.getActiveAccount();
  if (!account) return config;

  try {
    const tokenResponse = await msalInstance.acquireTokenSilent({
      account,
      scopes: loginRequest.scopes,
    });
    config.headers.Authorization = `Bearer ${tokenResponse.accessToken}`;
  } catch (error) {
    if (error instanceof InteractionRequiredAuthError) {
      await msalInstance.loginRedirect(loginRequest);
    }
  }
  return config;
});

export default apiClient;
