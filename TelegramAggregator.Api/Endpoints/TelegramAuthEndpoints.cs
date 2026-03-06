using Microsoft.AspNetCore.Mvc;
using TelegramAggregator.Api.Services;

namespace TelegramAggregator.Api.Endpoints;

public static class TelegramAuthEndpoints
{
    public static RouteGroupBuilder MapTelegramAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/telegram/auth")
            .WithTags("Telegram Authentication")
            .WithOpenApi();

        // POST /api/telegram/auth/start
        group.MapPost("/start", async (
            [FromBody] StartLoginRequest request,
            [FromServices] TelegramAuthService authService,
            [FromServices] WTelegramClientAdapter clientAdapter) =>
        {
            try
            {
                authService.StartLogin(clientAdapter.Client, request.PhoneNumber);
                return Results.Ok(new { message = "Login started" });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("StartTelegramLogin")
        .WithSummary("Initiate Telegram login flow with phone number");

        // GET /api/telegram/auth/status
        group.MapGet("/status", (
            [FromServices] TelegramAuthService authService) =>
        {
            return Results.Ok(new TelegramAuthStatusResponse
            {
                State = authService.CurrentState.ToString(),
                Prompt = authService.CurrentPrompt,
                IsInProgress = authService.IsLoginInProgress,
                ErrorMessage = authService.ErrorMessage
            });
        })
        .WithName("GetTelegramAuthStatus")
        .WithSummary("Get current Telegram authentication status");

        // POST /api/telegram/auth/submit
        group.MapPost("/submit", (
            [FromBody] SubmitInputRequest request,
            [FromServices] TelegramAuthService authService) =>
        {
            try
            {
                authService.ProvideInput(request.Input);
                return Results.Ok(new { message = "Input accepted" });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("SubmitTelegramAuthInput")
        .WithSummary("Submit verification code or password");

        // POST /api/telegram/auth/reset
        group.MapPost("/reset", (
            [FromServices] TelegramAuthService authService) =>
        {
            authService.Reset();
            return Results.Ok(new { message = "Auth state reset" });
        })
        .WithName("ResetTelegramAuth")
        .WithSummary("Reset authentication state (for debugging)");

        return group;
    }
}

// Request/Response DTOs
public record StartLoginRequest(string PhoneNumber);
public record SubmitInputRequest(string Input);
public record TelegramAuthStatusResponse
{
    public required string State { get; init; }
    public string? Prompt { get; init; }
    public bool IsInProgress { get; init; }
    public string? ErrorMessage { get; init; }
}
