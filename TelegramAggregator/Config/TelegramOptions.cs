namespace TelegramAggregator.Config;

public class TelegramOptions
{
    public string BotToken { get; set; } = string.Empty;
    public string ApiId { get; set; } = string.Empty;
    public string ApiHash { get; set; } = string.Empty;
    public string UserPhoneNumber { get; set; } = string.Empty;
    /// <summary>Path for WTelegramClient session file. Defaults to "telegram.session".</summary>
    public string SessionPath { get; set; } = "telegram.session";
}
