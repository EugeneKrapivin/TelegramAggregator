import { useState, useEffect } from 'react';

type LoginState =
  | 'NotStarted'
  | 'RequestingPhoneNumber'
  | 'AwaitingCode'
  | 'AwaitingPassword'
  | 'AwaitingName'
  | 'LoggedIn'
  | 'Failed';

interface AuthStatus {
  state: LoginState;
  prompt: string | null;
  isInProgress: boolean;
  errorMessage: string | null;
}

export default function TelegramLoginPage() {
  const [phoneNumber, setPhoneNumber] = useState('+');
  const [inputValue, setInputValue] = useState('');
  const [status, setStatus] = useState<AuthStatus>({
    state: 'NotStarted',
    prompt: null,
    isInProgress: false,
    errorMessage: null,
  });
  const [loading, setLoading] = useState(false);

  // Poll status every 2 seconds when login is in progress
  useEffect(() => {
    if (!status.isInProgress) return;

    const interval = setInterval(async () => {
      try {
        const response = await fetch('/api/telegram/auth/status');
        const data = await response.json();
        setStatus(data);

        // Clear input field when state changes
        if (data.state !== status.state) {
          setInputValue('');
        }
      } catch (error) {
        console.error('Failed to fetch status:', error);
      }
    }, 2000);

    return () => clearInterval(interval);
  }, [status.isInProgress, status.state]);

  const handleStartLogin = async () => {
    setLoading(true);
    try {
      const response = await fetch('/api/telegram/auth/start', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ phoneNumber }),
      });

      if (response.ok) {
        // Status will be updated by polling
        setStatus({ ...status, isInProgress: true });
      } else {
        const error = await response.json();
        setStatus({ ...status, errorMessage: error.error, state: 'Failed' });
      }
    } catch (error) {
      setStatus({ ...status, errorMessage: 'Network error', state: 'Failed' });
    } finally {
      setLoading(false);
    }
  };

  const handleSubmitInput = async () => {
    if (!inputValue.trim()) return;

    setLoading(true);
    try {
      const response = await fetch('/api/telegram/auth/submit', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ input: inputValue }),
      });

      if (response.ok) {
        setInputValue('');
        // Status will be updated by polling
      } else {
        const error = await response.json();
        alert(error.error);
      }
    } catch (error) {
      alert('Failed to submit input');
    } finally {
      setLoading(false);
    }
  };

  const getStateInfo = (): { title: string; description: string; icon: string } => {
    switch (status.state) {
      case 'NotStarted':
        return {
          title: 'Ready to Login',
          description: 'Enter your phone number to begin Telegram authentication',
          icon: '📱',
        };
      case 'AwaitingCode':
        return {
          title: 'Verification Code Required',
          description: 'Check your Telegram app or SMS for the verification code',
          icon: '🔐',
        };
      case 'AwaitingPassword':
        return {
          title: '2FA Password Required',
          description: 'Your account has two-factor authentication enabled',
          icon: '🔒',
        };
      case 'AwaitingName':
        return {
          title: 'Account Registration',
          description: 'This phone number is new. Enter your first and last name (e.g., "John Doe")',
          icon: '✏️',
        };
      case 'LoggedIn':
        return {
          title: 'Login Successful!',
          description: 'You are now authenticated with Telegram',
          icon: '✅',
        };
      case 'Failed':
        return {
          title: 'Login Failed',
          description: status.errorMessage || 'An unknown error occurred',
          icon: '❌',
        };
      default:
        return {
          title: 'Processing...',
          description: 'Please wait',
          icon: '⏳',
        };
    }
  };

  const stateInfo = getStateInfo();
  const showPhoneInput = status.state === 'NotStarted';
  const showCodeInput = ['AwaitingCode', 'AwaitingPassword', 'AwaitingName'].includes(status.state);
  const isComplete = status.state === 'LoggedIn';

  return (
    <div className="min-h-screen bg-zinc-950 flex items-center justify-center p-4">
      <div className="w-full max-w-md">
        {/* Card */}
        <div className="bg-zinc-900 border border-zinc-800 rounded-xl shadow-2xl overflow-hidden">
          {/* Header */}
          <div className="px-6 py-5 border-b border-zinc-800">
            <div className="flex items-center gap-3">
              <span className="text-3xl">{stateInfo.icon}</span>
              <div>
                <h2 className="text-lg font-semibold text-zinc-100" style={{ fontFamily: "'Exo 2', sans-serif" }}>
                  {stateInfo.title}
                </h2>
                <p className="text-sm text-zinc-400 mt-0.5">{stateInfo.description}</p>
              </div>
            </div>
          </div>

          {/* Content */}
          <div className="px-6 py-5 space-y-4">
            {showPhoneInput && (
              <>
                <div>
                  <label className="block text-sm font-medium text-zinc-300 mb-2">Phone Number</label>
                  <input
                    type="tel"
                    placeholder="+1234567890"
                    value={phoneNumber}
                    onChange={(e) => setPhoneNumber(e.target.value)}
                    disabled={loading}
                    className="w-full px-3 py-2 bg-zinc-800 border border-zinc-700 rounded-lg text-zinc-100 placeholder-zinc-500 focus:outline-none focus:ring-2 focus:ring-cyan-500 focus:border-transparent disabled:opacity-50"
                  />
                  <p className="text-xs text-zinc-500 mt-1.5">Include country code (e.g., +1 for US)</p>
                </div>
                <button
                  onClick={handleStartLogin}
                  disabled={loading || phoneNumber.length < 5}
                  className="w-full px-4 py-2.5 bg-cyan-500 text-zinc-950 font-semibold rounded-lg hover:bg-cyan-400 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {loading ? 'Starting...' : 'Start Login'}
                </button>
              </>
            )}

            {showCodeInput && (
              <>
                <div>
                  <label className="block text-sm font-medium text-zinc-300 mb-2">
                    {status.state === 'AwaitingCode'
                      ? 'Verification Code'
                      : status.state === 'AwaitingPassword'
                      ? '2FA Password'
                      : 'Full Name'}
                  </label>
                  <input
                    type={status.state === 'AwaitingPassword' ? 'password' : 'text'}
                    placeholder={
                      status.state === 'AwaitingCode'
                        ? 'Enter 5-digit code'
                        : status.state === 'AwaitingPassword'
                        ? 'Enter 2FA password'
                        : 'John Doe'
                    }
                    value={inputValue}
                    onChange={(e) => setInputValue(e.target.value)}
                    onKeyDown={(e) => e.key === 'Enter' && handleSubmitInput()}
                    disabled={loading}
                    autoFocus
                    className="w-full px-3 py-2 bg-zinc-800 border border-zinc-700 rounded-lg text-zinc-100 placeholder-zinc-500 focus:outline-none focus:ring-2 focus:ring-cyan-500 focus:border-transparent disabled:opacity-50"
                  />
                </div>
                <button
                  onClick={handleSubmitInput}
                  disabled={loading || !inputValue.trim()}
                  className="w-full px-4 py-2.5 bg-cyan-500 text-zinc-950 font-semibold rounded-lg hover:bg-cyan-400 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {loading ? 'Submitting...' : 'Submit'}
                </button>
              </>
            )}

            {isComplete && (
              <div className="p-4 bg-green-500/10 border border-green-500/25 rounded-lg">
                <p className="text-sm text-green-400">
                  ✅ Session saved. The server will now auto-connect on future restarts.
                </p>
              </div>
            )}

            {status.state === 'Failed' && (
              <div className="p-4 bg-red-500/10 border border-red-500/25 rounded-lg">
                <p className="text-sm text-red-400">❌ {status.errorMessage}</p>
              </div>
            )}
          </div>
        </div>

        {/* Back link */}
        <div className="mt-4 text-center">
          <a href="/" className="text-sm text-zinc-400 hover:text-zinc-300 transition-colors">
            ← Back to Channels
          </a>
        </div>
      </div>
    </div>
  );
}
