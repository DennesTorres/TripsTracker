import { useMsal } from '@azure/msal-react';
import { loginRequest } from './authConfig';

export default function LoginPage() {
  const { instance } = useMsal();

  const handleSignIn = () => {
    instance.loginRedirect(loginRequest);
  };

  return (
    <div style={{
      minHeight: '100vh',
      background: 'linear-gradient(160deg, #0f1724 0%, #1a2a4a 60%, #0f1724 100%)',
      color: '#e0e0e0',
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      justifyContent: 'center',
      padding: '2rem',
      fontFamily: 'system-ui, sans-serif',
    }}>

      {/* Globe icon placeholder */}
      <div style={{
        fontSize: '4rem',
        marginBottom: '1.5rem',
        filter: 'drop-shadow(0 0 20px rgba(0,120,212,0.4))',
      }}>
        🌍
      </div>

      <h1 style={{
        fontSize: 'clamp(2rem, 5vw, 3.5rem)',
        fontWeight: 700,
        margin: '0 0 0.5rem',
        background: 'linear-gradient(90deg, #60a5fa, #a78bfa)',
        WebkitBackgroundClip: 'text',
        WebkitTextFillColor: 'transparent',
        textAlign: 'center',
      }}>
        TripsTracker
      </h1>

      <p style={{
        fontSize: '1.15rem',
        color: '#94a3b8',
        margin: '0 0 0.75rem',
        textAlign: 'center',
      }}>
        Your personal world travel map
      </p>

      <p style={{
        fontSize: '0.95rem',
        color: '#64748b',
        margin: '0 0 3rem',
        maxWidth: '420px',
        textAlign: 'center',
        lineHeight: 1.6,
      }}>
        Pin the cities you've visited, track the countries and states you've explored,
        and watch your world fill up over time.
      </p>

      <button
        onClick={handleSignIn}
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: '0.6rem',
          padding: '0.85rem 2.25rem',
          fontSize: '1rem',
          fontWeight: 600,
          background: '#0078d4',
          color: '#fff',
          border: 'none',
          borderRadius: '8px',
          cursor: 'pointer',
          transition: 'background 0.15s',
          boxShadow: '0 4px 20px rgba(0,120,212,0.3)',
        }}
        onMouseOver={e => (e.currentTarget.style.background = '#106ebe')}
        onMouseOut={e => (e.currentTarget.style.background = '#0078d4')}
      >
        <MicrosoftLogo />
        Continue with Microsoft
      </button>

      <p style={{
        marginTop: '1.25rem',
        fontSize: '0.8rem',
        color: '#475569',
        textAlign: 'center',
      }}>
        New or returning — one click is all it takes
      </p>
    </div>
  );
}

function MicrosoftLogo() {
  return (
    <svg width="18" height="18" viewBox="0 0 21 21" fill="none" xmlns="http://www.w3.org/2000/svg">
      <rect x="1" y="1" width="9" height="9" fill="#f25022"/>
      <rect x="11" y="1" width="9" height="9" fill="#7fba00"/>
      <rect x="1" y="11" width="9" height="9" fill="#00a4ef"/>
      <rect x="11" y="11" width="9" height="9" fill="#ffb900"/>
    </svg>
  );
}
