using System;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Storage.Queues.Models;
using CoffeeNChill.Functions.Models;
using CoffeeNChill.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CoffeeNChill.Functions.Functions
{
    /// <summary>
    /// Queue-triggered Azure Function that processes incoming order messages
    /// from the "order-processing-queue".
    ///
    /// Flow:
    ///   1. A message arrives in the queue (produced by the Order Queue Producer API).
    ///   2. This function fires automatically with the message as a QueueMessage.
    ///   3. The JSON payload is deserialized into an OrderRequest DTO.
    ///   4. The order is validated and persisted to the Orders table with status "Received".
    ///   5. If deserialization or validation fails, the message moves to the poison queue.
    /// </summary>
    public class OrderQueueFunctions
    {
        private readonly ILogger<OrderQueueFunctions> _logger;
        private readonly OrderStorageService _orderStorage;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Constructor injects the logger and Orders storage service.
        /// </summary>
        public OrderQueueFunctions(
            ILogger<OrderQueueFunctions> logger,
            OrderStorageService orderStorage)
        {
            _logger = logger;
            _orderStorage = orderStorage;
        }

        /// <summary>
        /// Triggered automatically when a message is added to "order-processing-queue".
        /// Parses the JSON, validates the order, and saves it to Table Storage.
        /// </summary>
        /// <param name="message">The queue message containing the order JSON.</param>
        [Function("ProcessOrderQueue")]
        public async Task ProcessOrderQueue(
            [QueueTrigger("order-processing-queue", Connection = "AzureWebJobsStorage")]
            QueueMessage message)
        {
            _logger.LogInformation(
                "Received message from order-processing-queue. MessageId: {MessageId}, DequeueCount: {DequeueCount}",
                message.MessageId, message.DequeueCount);

            OrderRequest? request;

            // Step 1: Try to deserialize the JSON message
            try
            {
                request = JsonSerializer.Deserialize<OrderRequest>(message.MessageText, _jsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "Failed to deserialize message {MessageId}. Moving to poison queue.",
                    message.MessageId);
                throw; // Throwing causes the message to be retried and then poison-queued
            }

            // Step 2: Validate the deserialized request
            if (request == null)
            {
                _logger.LogError("Order request was null after deserialization. MessageId: {MessageId}",
                    message.MessageId);
                throw new InvalidOperationException("Order request was null.");
            }

            if (string.IsNullOrWhiteSpace(request.CustomerName) ||
                string.IsNullOrWhiteSpace(request.CustomerPhone) ||
                request.Items == null ||
                request.Items.Count == 0)
            {
                _logger.LogError(
                    "Invalid order data received. CustomerName: {CustomerName}, Items count: {ItemCount}",
                    request.CustomerName, request.Items?.Count ?? 0);
                throw new InvalidOperationException("Order request failed validation.");
            }

            // Step 3: Map to the Order entity and persist to Table Storage
            var order = new Order
            {
                OrderId = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                CustomerName = request.CustomerName.Trim(),
                CustomerPhone = request.CustomerPhone.Trim(),
                ItemsJson = JsonSerializer.Serialize(request.Items, _jsonOptions),
                TotalPrice = request.TotalPrice,
                Notes = request.Notes?.Trim() ?? string.Empty,
                Status = OrderStatus.Received.ToString(),
                ReceivedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };

            await _orderStorage.CreateOrderAsync(order);

            _logger.LogInformation(
                "Successfully processed order {OrderId} for customer {Customer}.",
                order.OrderId, order.CustomerName);
        }
    }
}