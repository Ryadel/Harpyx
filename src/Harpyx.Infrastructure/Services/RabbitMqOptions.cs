namespace Harpyx.Infrastructure.Services;

public record RabbitMqOptions
{
    public string HostName { get; init; } = "localhost";
    public string UserName { get; init; } = "guest";
    public string Password { get; init; } = "guest";
}
