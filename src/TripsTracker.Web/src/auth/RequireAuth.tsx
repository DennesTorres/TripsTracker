import type { ReactNode } from 'react';
import { AuthenticatedTemplate, UnauthenticatedTemplate } from '@azure/msal-react';
import LoginPage from './LoginPage';

interface RequireAuthProps {
  children: ReactNode;
}

/**
 * Renders children when the user is authenticated.
 * Shows the login page when not authenticated.
 */
export function RequireAuth({ children }: RequireAuthProps) {
  return (
    <>
      <AuthenticatedTemplate>{children}</AuthenticatedTemplate>
      <UnauthenticatedTemplate><LoginPage /></UnauthenticatedTemplate>
    </>
  );
}
