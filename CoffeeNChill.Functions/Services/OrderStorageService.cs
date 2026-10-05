using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using CoffeeNChill.Functions.Models;
using Microsoft.Extensions.Logging;

namespace CoffeeNChill.Functions.Services
{
    /// <summary>
    /// Data access layer for the "Orders" table in Azure Table Storage.
    /// Handles creating orders, retrieving them by ID or customer, and updating
    /// their status as they move through the lifecycle (Received → Preparing → Ready → Collected).
    /// </summary>
    public class OrderStorageService
    {
        private const string TableName = "Orders";
        private readonly TableClient _tableClient;
        private readonly ILogger<OrderStorageService>? _logger;

        /// <summary>
        /// Initializes the service and ensures the "Orders" table exists.
        /// </summary>
        /// <param name="connectionString">Azure Storage connection string (Azurite or real Azure).</param>
        /// <param name="logger">Optional logger for diagnostics.</param>
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
        /// <param name="order">The order entity to persist.</param>
        /// <returns>The persisted order with its generated identifiers.</returns>
        public async Task<Order> CreateOrderAsync(Order order)
        {
            try
            {
                // Ensure the initial status is "Received"
                if (string.IsNullOrWhiteSpace(order.Status))
                {
                    order.Status = OrderStatus.Received.ToString();
                }

                order.ReceivedAt = DateTime.UtcNow;
                order.LastUpdatedAt = DateTime.UtcNow;

                await _tableClient.AddEntityAsync(order);

                _logger?.LogInformation(
                    "Created order {OrderId} ({RowKey}) for customer {Customer}",
                    order.OrderId, order.RowKey, order.CustomerName);

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
        /// Retrieves a single order by its unique OrderId (stored in the RowKey).
        /// Returns null if the order is not found.
        /// </summary>
        /// <param name="orderId">The order's unique GUID identifier (RowKey).</param>
        /// <returns>The matching order, or null if not found.</returns>
        public async Task<Order?> GetOrderByIdAsync(string orderId)
        {
            try
            {
                var response = await _tableClient.GetEntityAsync<Order>("Order", orderId);
                _logger?.LogInformation("Retrieved order {OrderId}.", orderId);
                return response.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
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
        /// Retrieves all orders in the table. Useful for admin dashboards
        /// that need to display the full order list.
        /// </summary>
        /// <returns>A list of all orders in storage.</returns>
        public async Task<List<Order>> GetAllOrdersAsync()
        {
            var results = new List<Order>();
            try
            {
                await foreach (var order in _tableClient.QueryAsync<Order>(_ => true))
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
        /// Retrieves all orders that currently have a specific status
        /// (e.g., all orders that are "Ready" for collection).
        /// </summary>
        /// <param name="status">The status to filter by (use OrderStatus enum values).</param>
        /// <returns>A list of orders matching the status.</returns>
        public async Task<List<Order>> GetOrdersByStatusAsync(OrderStatus status)
        {
            var results = new List<Order>();
            var statusStr = status.ToString();

            try
            {
                await foreach (var order in _tableClient.QueryAsync<Order>(
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
        /// Updates the status of an existing order and refreshes the LastUpdatedAt timestamp.
        /// Used to transition orders through Received → Preparing → Ready → Collected.
        /// </summary>
        /// <param name="orderId">The order's unique GUID identifier (RowKey).</param>
        /// <param name="newStatus">The new status to apply.</param>
        /// <returns>The updated order, or null if the order was not found.</returns>
        public async Task<Order?> UpdateOrderStatusAsync(string orderId, OrderStatus newStatus)
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