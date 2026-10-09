using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using CoffeeNChill.Functions.Models;
using Microsoft.Extensions.Logging;
/// written by st10478910
namespace CoffeeNChill.Functions.Services
{
    /// <summary>
    /// Data access layer for the "Orders" table in Azure Table Storage.
    /// Handles creating orders, retrieving them by ID or status, and updating
    /// their status as they move through the lifecycle (Received → Preparing → Ready → Collected).
    ///
    /// Table design:
    ///   PartitionKey = OrderDate (yyyy-MM-dd)
    ///   RowKey       = OrderId (e.g., "ORD-2026-8801")
    /// </summary>
    public class OrderStorageService
    {
        private const string TableName = "Orders";
        private readonly TableClient _tableClient;
        private readonly ILogger<OrderStorageService>? _logger;

        /// <summary>
        /// Initializes the service and ensures the "Orders" table exists.
        /// </summary>
        public OrderStorageService(string connectionString, ILogger<OrderStorageService>? logger = null)
        {
            _logger = logger;

            try
            {
                var serviceClient = new TableServiceClient(connectionString);
                _tableClient = serviceClient.GetTableClient(TableName);
                _tableClient.CreateIfNotExists();
                _logger?.LogInformation("Orders table initialized successfully.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize Orders table.");
                throw;
            }
        }

        /// <summary>
        /// Creates a new order record in the Orders table.
        /// Called after a message is received from the order-processing-queue.
        /// </summary>
        public async Task<OrderEntity> CreateOrderAsync(OrderEntity order)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(order.Status))
                {
                    order.Status = OrderStatus.Received.ToString();
                }

                order.ReceivedAt = DateTime.UtcNow;
                order.LastUpdatedAt = DateTime.UtcNow;

                await _tableClient.AddEntityAsync(order);

                _logger?.LogInformation(
                    "Created order {OrderId} ({PartitionKey}/{RowKey}) for customer {Customer}",
                    order.OrderId, order.PartitionKey, order.RowKey, order.CustomerName);

                return order;
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                _logger?.LogWarning("Order {OrderId} already exists.", order.OrderId);
                throw new InvalidOperationException(
                    $"Order '{order.OrderId}' already exists.", ex);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to create order {OrderId}.", order.OrderId);
                throw;
            }
        }

        /// <summary>
        /// Retrieves a single order by its OrderId (RowKey).
        /// Because PartitionKey = OrderDate, this method queries across ALL partitions
        /// and filters by RowKey. If you know the OrderDate, use GetOrderByIdAsync(orderDate, orderId)
        /// instead for a faster point lookup.
        /// </summary>
        /// <param name="orderId">The order's unique identifier (RowKey).</param>
        /// <returns>The matching order, or null if not found.</returns>
        public async Task<OrderEntity?> GetOrderByIdAsync(string orderId)
        {
            try
            {
                // Scan across all partitions filtering by RowKey.
                // This is acceptable for a demo project; in production you would
                // store the OrderDate and do a point lookup.
                await foreach (var order in _tableClient.QueryAsync<OrderEntity>(
                    filter: o => o.RowKey == orderId))
                {
                    _logger?.LogInformation("Retrieved order {OrderId}.", orderId);
                    return order;
                }

                _logger?.LogInformation("Order {OrderId} not found.", orderId);
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error retrieving order {OrderId}.", orderId);
                throw;
            }
        }

        /// <summary>
        /// Fast point-lookup when you know both the OrderDate (PartitionKey) and OrderId (RowKey).
        /// </summary>
        /// <param name="orderDate">Order date in "yyyy-MM-dd" format.</param>
        /// <param name="orderId">The order's unique identifier (RowKey).</param>
        /// <returns>The matching order, or null if not found.</returns>
        public async Task<OrderEntity?> GetOrderByIdAsync(string orderDate, string orderId)
        {
            try
            {
                var response = await _tableClient.GetEntityAsync<OrderEntity>(orderDate, orderId);
                _logger?.LogInformation(
                    "Retrieved order {OrderId} from partition {PartitionKey}.", orderId, orderDate);
                return response.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger?.LogInformation(
                    "Order {OrderId} not found in partition {PartitionKey}.", orderId, orderDate);
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex,
                    "Error retrieving order {OrderId} from partition {PartitionKey}.", orderId, orderDate);
                throw;
            }
        }

        /// <summary>
        /// Retrieves all orders across every partition (every date).
        /// </summary>
        public async Task<List<OrderEntity>> GetAllOrdersAsync()
        {
            var results = new List<OrderEntity>();
            try
            {
                await foreach (var order in _tableClient.QueryAsync<OrderEntity>(_ => true))
                {
                    results.Add(order);
                }

                _logger?.LogInformation("Retrieved {Count} orders.", results.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to retrieve all orders.");
                throw;
            }

            return results;
        }

        /// <summary>
        /// Retrieves all orders with a specific status (e.g., all "Ready" orders).
        /// </summary>
        public async Task<List<OrderEntity>> GetOrdersByStatusAsync(OrderStatus status)
        {
            var results = new List<OrderEntity>();
            var statusStr = status.ToString();

            try
            {
                await foreach (var order in _tableClient.QueryAsync<OrderEntity>(
                    o => o.Status == statusStr))
                {
                    results.Add(order);
                }

                _logger?.LogInformation(
                    "Retrieved {Count} orders with status {Status}.",
                    results.Count, statusStr);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex,
                    "Failed to retrieve orders with status {Status}.", statusStr);
                throw;
            }

            return results;
        }

        /// <summary>
        /// Updates the status of an existing order and refreshes LastUpdatedAt.
        /// </summary>
        public async Task<OrderEntity?> UpdateOrderStatusAsync(string orderId, OrderStatus newStatus)
        {
            try
            {
                var existing = await GetOrderByIdAsync(orderId);
                if (existing == null)
                {
                    _logger?.LogWarning(
                        "Cannot update status - order {OrderId} not found.", orderId);
                    return null;
                }

                var oldStatus = existing.Status;
                existing.Status = newStatus.ToString();
                existing.LastUpdatedAt = DateTime.UtcNow;

                await _tableClient.UpdateEntityAsync(existing, ETag.All, TableUpdateMode.Replace);

                _logger?.LogInformation(
                    "Order {OrderId} status updated from {OldStatus} to {NewStatus}.",
                    orderId, oldStatus, existing.Status);

                return existing;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex,
                    "Failed to update status for order {OrderId}.", orderId);
                throw;
            }
        }
    }
}
