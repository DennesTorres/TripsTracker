import { useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { MsalProvider } from '@azure/msal-react';
import { msalInstance, msalInitialized } from './msalInstance';

interface AuthProviderProps {
  children: ReactNode;
}

export function AuthProvider({ children }: AuthProviderProps) {
  const [ready, setReady] = useState(false);

  useEffect(() => {
    msalInitialized.then(() => setReady(true));
  }, []);

  if (!ready) {
    return null; // Brief pause while MSAL initializes
  }

  return <MsalProvider instance={msalInstance}>{children}</MsalProvider>;
}
