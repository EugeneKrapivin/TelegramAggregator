using Microsoft.Extensions.Logging;

namespace TelegramAggregator.Api.Services;

public enum TelegramLoginState
{
    NotStarted,
    RequestingPhoneNumber,
    AwaitingCode,
    AwaitingPassword,
    AwaitingName,      // If signup required
    LoggedIn,
    Failed
}

public class TelegramAuthService
{
    private readonly ILogger<TelegramAuthService> _logger;
    private TaskCompletionSource<string>? _inputTcs;
    private Task? _loginTask;
    private readonly object _lock = new();

    public TelegramLoginState CurrentState { get; private set; } = TelegramLoginState.NotStarted;
    public string? ErrorMessage { get; private set; }
    public string? CurrentPrompt { get; private set; }
    public bool IsLoginInProgress => _loginTask is { IsCompleted: false };

    // Thread-safe event for state changes
    public event Action<TelegramLoginState>? OnStateChanged;

    public TelegramAuthService(ILogger<TelegramAuthService> logger)
    {
        _logger = logger;
    }

    // Called by minimal API endpoint
    public void StartLogin(WTelegram.Client client, string phoneNumber)
    {
        lock (_lock)
        {
            if (IsLoginInProgress)
                throw new InvalidOperationException("Login already in progress");

            _loginTask = Task.Run(async () => await ExecuteLoginLoop(client, phoneNumber));
        }
    }

    // The core login loop using client.Login()
    private async Task ExecuteLoginLoop(WTelegram.Client client, string initialInput)
    {
        try
        {
            SetState(TelegramLoginState.RequestingPhoneNumber);
            string? loginInfo = initialInput;

            while (client.User == null)
            {
                var prompt = await client.Login(loginInfo);
                _logger.LogInformation("Login prompt: {Prompt}", prompt);

                if (prompt == null)
                {
                    SetState(TelegramLoginState.LoggedIn);
                    _logger.LogInformation("Login successful: {User}", client.User);
                    break;
                }

                // Map WTelegramClient prompts to our state enum
                CurrentPrompt = prompt;
                SetState(prompt switch
                {
                    "verification_code" => TelegramLoginState.AwaitingCode,
                    "password" => TelegramLoginState.AwaitingPassword,
                    "first_name" => TelegramLoginState.AwaitingName,
                    "last_name" => TelegramLoginState.AwaitingName,
                    _ => TelegramLoginState.Failed
                });

                if (CurrentState == TelegramLoginState.Failed)
                {
                    ErrorMessage = $"Unknown prompt: {prompt}";
                    break;
                }

                // Wait for user input via HTTP endpoint
                loginInfo = await WaitForUserInput();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed");
            ErrorMessage = ex.Message;
            SetState(TelegramLoginState.Failed);
        }
    }

    // Called by minimal API endpoint when user submits code/password
    public void ProvideInput(string input)
    {
        lock (_lock)
        {
            if (_inputTcs == null || _inputTcs.Task.IsCompleted)
                throw new InvalidOperationException("No input currently expected");

            _inputTcs.SetResult(input);
        }
    }

    private async Task<string> WaitForUserInput()
    {
        lock (_lock)
        {
            _inputTcs = new TaskCompletionSource<string>();
        }
        return await _inputTcs.Task;
    }

    private void SetState(TelegramLoginState newState)
    {
        CurrentState = newState;
        OnStateChanged?.Invoke(newState);
        _logger.LogInformation("Login state changed to: {State}", newState);
    }

    public void Reset()
    {
        lock (_lock)
        {
            _inputTcs?.TrySetCanceled();
            _inputTcs = null;
            _loginTask = null;
            CurrentState = TelegramLoginState.NotStarted;
            ErrorMessage = null;
            CurrentPrompt = null;
        }
    }
}
