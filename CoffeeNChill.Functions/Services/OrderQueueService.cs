using System.Text.Json;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using CoffeeNChill.Functions.Models;

namespace CoffeeNChill.Functions.Services;

/// <summary>
/// Provides operations for submitting CoffeeNChill orders to Azure Queue
/// Storage for asynchronous processing.
/// </summary>
/// <remarks>
/// Owner: Douglass Duncan — ST10473980
/// </remarks>
public class OrderQueueService
{
    private const string QueueName = "order-processing-queue";

    private readonly QueueClient _queueClient;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderQueueService"/> class
    /// using the configured Azure Storage connection string.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the <c>AzureWebJobsStorage</c> environment variable is
    /// missing or empty.
    /// </exception>
    public OrderQueueService()
    {
        string connectionString =
            Environment.GetEnvironmentVariable("AzureWebJobsStorage")
            ?? throw new InvalidOperationException(
                "The AzureWebJobsStorage connection string is required.");

        var queueOptions = new QueueClientOptions(
            QueueClientOptions.ServiceVersion.V2026_06_06)
        {
            MessageEncoding = QueueMessageEncoding.Base64
        };

        _queueClient = new QueueClient(
            connectionString,
            QueueName,
            queueOptions);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Serializes an order to JSON and submits it to the
    /// <c>order-processing-queue</c>.
    /// </summary>
    /// <param name="order">The completed order to place onto the queue.</param>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>A task representing the asynchronous queue operation.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="order"/> is <see langword="null"/>.
    /// </exception>
    public async Task EnqueueOrderAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        await _queueClient.CreateIfNotExistsAsync(
            cancellationToken: cancellationToken);

        string orderJson = JsonSerializer.Serialize(order, _jsonOptions);

        await _queueClient.SendMessageAsync(
            orderJson,
            cancellationToken);
    }
}