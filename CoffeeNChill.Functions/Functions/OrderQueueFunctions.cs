using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Storage.Queues.Models;
using CoffeeNChill.Functions.Models;
using CoffeeNChill.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
/// written by st10478910
namespace CoffeeNChill.Functions.Functions
{
    /// <summary>
    /// Queue-triggered Azure Function that processes incoming order messages
    /// from the "order-processing-queue".
    ///
    /// Flow:
    ///   1. A message arrives in the queue (produced by the Order Queue Producer API).
    ///   2. This function fires automatically with the message as a QueueMessage.
    ///   3. The JSON payload is deserialized into an OrderMessage DTO.
    ///   4. The order is validated and persisted to the Orders table with status "Received".
    ///   5. If deserialization or validation fails, the message moves to the poison queue.
    /// </summary>
    public class OrderQueueFunctions
    {
        private readonly ILogger<OrderQueueFunctions> _logger;
        private readonly OrderStorageService _orderStorage;

        // PropertyNameCaseInsensitive = true is REQUIRED so that camelCase JSON
        // (orderId, customerName, selectedItemSKUs, ...) maps correctly to PascalCase properties.
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

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
        [Function("ProcessOrderQueue")]
        public async Task ProcessOrderQueue(
            [QueueTrigger("order-processing-queue", Connection = "AzureWebJobsStorage")]
            QueueMessage message)
        {
            _logger.LogInformation(
                "Received message from order-processing-queue. MessageId: {MessageId}, DequeueCount: {DequeueCount}",
                message.MessageId, message.DequeueCount);

            OrderMessage? orderMessage;

            // Step 1: Deserialize the JSON
            try
            {
                orderMessage = JsonSerializer.Deserialize<OrderMessage>(message.MessageText, _jsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "Failed to deserialize message {MessageId}. Moving to poison queue.",
                    message.MessageId);
                throw;
            }

            // Step 2: Validate
            if (orderMessage == null)
            {
                _logger.LogError("Order message was null after deserialization. MessageId: {MessageId}",
                    message.MessageId);
                throw new InvalidOperationException("Order message was null.");
            }

            if (string.IsNullOrWhiteSpace(orderMessage.OrderId) ||
                string.IsNullOrWhiteSpace(orderMessage.CustomerName) ||
                orderMessage.SelectedItemSKUs == null ||
                orderMessage.SelectedItemSKUs.Count == 0)
            {
                _logger.LogError(
                    "Invalid order data received. OrderId: {OrderId}, CustomerName: {CustomerName}, SKU count: {SkuCount}",
                    orderMessage.OrderId, orderMessage.CustomerName,
                    orderMessage.SelectedItemSKUs?.Count ?? 0);
                throw new InvalidOperationException("Order message failed validation.");
            }

            // Step 3: Map to OrderEntity and persist
            var order = new OrderEntity
            {
                // PartitionKey = OrderDate (yyyy-MM-dd)
                PartitionKey = orderMessage.OrderTimestamp.UtcDateTime.ToString("yyyy-MM-dd"),
                // RowKey = OrderId
                RowKey = orderMessage.OrderId,
                OrderId = orderMessage.OrderId,
                CustomerName = orderMessage.CustomerName.Trim(),
                SelectedItemSKUs = string.Join(",", orderMessage.SelectedItemSKUs),
                TotalPrice = (double)orderMessage.TotalPrice,
                Status = OrderStatus.Received.ToString(),
                OrderTimestamp = orderMessage.OrderTimestamp,
                ReceivedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };

            await _orderStorage.CreateOrderAsync(order);

            _logger.LogInformation(
                "Successfully processed order {OrderId} for customer {Customer}.",
                order.OrderId, order.CustomerName);
        }

        /// <summary>
        /// Handles messages that have failed processing 5 times and been moved
        /// to the poison queue (order-processing-queue-poison).
        /// </summary>
        [Function("ProcessOrderQueuePoison")]
        public void ProcessOrderQueuePoison(
            [QueueTrigger("order-processing-queue-poison", Connection = "AzureWebJobsStorage")]
            QueueMessage poisonMessage)
        {
            _logger.LogError(
                "POISON MESSAGE received. MessageId: {MessageId}, DequeueCount: {DequeueCount}, Body: {Body}",
                poisonMessage.MessageId,
                poisonMessage.DequeueCount,
                poisonMessage.MessageText);
        }
    }
}