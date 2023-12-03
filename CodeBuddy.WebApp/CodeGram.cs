namespace CodeBuddy.WebApp;

public enum EventType
{
    Connect,
    Message,
    Disconnect
}

public class CodeGram
{
    public string? Board { get; set; }
    public string UserId { get; set; }
    public EventType Type { get; set; }
    public string Data { get; set; }
}