using System.Text;
using Harpyx.Application.Interfaces;
using Harpyx.Shared;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Harpyx.Infrastructure.Services;

public class RabbitMqJobQueue : IJobQueue, IDisposable
{
    private const int MaxRetries = 3;
    private readonly IConnection _connection;
    private readonly IChannel _channel;

    public RabbitMqJobQueue(RabbitMqOptions options)
    {
        var factory = new ConnectionFactory
        {
            HostName = options.HostName,
            UserName = options.UserName,
            Password = options.Password
        };

        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
        _channel.QueueDeclareAsync(
            HarpyxConstants.JobQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = string.Empty,
                ["x-dead-letter-routing-key"] = HarpyxConstants.DeadLetterQueueName
            }).GetAwaiter().GetResult();
        _channel.QueueDeclareAsync(
            HarpyxConstants.DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false).GetAwaiter().GetResult();
    }

    public async Task EnqueueParseJobAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var body = Encoding.UTF8.GetBytes(documentId.ToString());
        var properties = CreateProperties(retryCount: 0);
        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: HarpyxConstants.JobQueueName,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }

    public async Task ConsumeAsync(Func<Guid, CancellationToken, Task> handler, CancellationToken cancellationToken)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var message = Encoding.UTF8.GetString(ea.Body.ToArray());
            if (!Guid.TryParse(message, out var documentId))
            {
                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken);
                return;
            }

            try
            {
                await handler(documentId, cancellationToken);
                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken);
            }
            catch (Exception)
            {
                var retries = 0;
                if (ea.BasicProperties?.Headers != null && ea.BasicProperties.Headers.TryGetValue("x-retry-count", out var value))
                {
                    retries = value switch
                    {
                        byte[] bytes => int.TryParse(Encoding.UTF8.GetString(bytes), out var parsed) ? parsed : 0,
                        int count => count,
                        long count => (int)count,
                        _ => 0
                    };
                }

                if (retries < MaxRetries)
                {
                    var properties = CreateProperties(retries + 1);
                    await _channel.BasicPublishAsync(
                        exchange: string.Empty,
                        routingKey: HarpyxConstants.JobQueueName,
                        mandatory: false,
                        basicProperties: properties,
                        body: ea.Body,
                        cancellationToken: cancellationToken);
                }
                else
                {
                    var properties = CreateProperties(retries);
                    await _channel.BasicPublishAsync(
                        exchange: string.Empty,
                        routingKey: HarpyxConstants.DeadLetterQueueName,
                        mandatory: false,
                        basicProperties: properties,
                        body: ea.Body,
                        cancellationToken: cancellationToken);
                }

                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: HarpyxConstants.JobQueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);
    }

    public void Dispose()
    {
        _channel.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _connection.Dispose();
    }

    private static BasicProperties CreateProperties(int retryCount)
        => new()
        {
            Persistent = true,
            Headers = new Dictionary<string, object?> { ["x-retry-count"] = retryCount }
        };
}
