using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CoffeeNChill.Functions.Models;
using CoffeeNChill.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace CoffeeNChill.Functions.Functions
{
    /// <summary>
    /// HTTP-triggered functions for viewing and updating order statuses.
    /// Allows staff to move orders through the lifecycle:
    /// Received → Preparing → Ready → Collected.
    /// </summary>
    public class OrderStatusFunctions
    {
        private readonly ILogger<OrderStatusFunctions> _logger;
        private readonly OrderStorageService _orderStorage;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public OrderStatusFunctions(
            ILogger<OrderStatusFunctions> logger,
            OrderStorageService orderStorage)
        {
            _logger = logger;
            _orderStorage = orderStorage;
        }

        /// <summary>
        /// GET /api/orders - Returns all orders currently stored in the system.
        /// </summary>
        [Function("GetAllOrders")]
        public async Task<HttpResponseData> GetAllOrders(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders")] HttpRequestData req)
        {
            try
            {
                _logger.LogInformation("Retrieving all orders.");

                var orders = await _orderStorage.GetAllOrdersAsync();

                var response = req.CreateResponse(HttpStatusCode.OK);
                string json = JsonSerializer.Serialize(orders, _jsonOptions);
                await response.WriteStringAsync(json, Encoding.UTF8);
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all orders.");
                return await CreateErrorResponse(req);
            }
        }

        /// <summary>
        /// GET /api/orders/{orderId} - Returns a single order by its ID.
        /// </summary>
        [Function("GetOrderById")]
        public async Task<HttpResponseData> GetOrderById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders/{orderId}")] HttpRequestData req,
            string orderId)
        {
            try
            {
                _logger.LogInformation("Retrieving order {OrderId}.", orderId);

                var order = await _orderStorage.GetOrderByIdAsync(orderId);
                if (order == null)
                {
                    return await CreateNotFoundResponse(req, $"Order '{orderId}' not found.");
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                string json = JsonSerializer.Serialize(order, _jsonOptions);
                await response.WriteStringAsync(json, Encoding.UTF8);
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving order {OrderId}.", orderId);
                return await CreateErrorResponse(req);
            }
        }

        /// <summary>
        /// PUT /api/orders/{orderId}/status - Updates an order's status to a specific value.
        /// Body must contain: { "status": "Preparing" | "Ready" | "Collected" }.
        /// </summary>
        [Function("UpdateOrderStatus")]
        public async Task<HttpResponseData> UpdateOrderStatus(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "orders/{orderId}/status")] HttpRequestData req,
            string orderId)
        {
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var body = JsonSerializer.Deserialize<UpdateStatusRequest>(requestBody, _jsonOptions);

                if (body == null || string.IsNullOrWhiteSpace(body.Status))
                {
                    return await CreateBadResponse(req, "Status field is required.");
                }

                // Validate the status against the enum
                if (!Enum.TryParse<OrderStatus>(body.Status, ignoreCase: true, out var newStatus))
                {
                    return await CreateBadResponse(req,
                        $"Invalid status '{body.Status}'. Must be one of: Received, Preparing, Ready, Collected.");
                }

                var updated = await _orderStorage.UpdateOrderStatusAsync(orderId, newStatus);
                if (updated == null)
                {
                    return await CreateNotFoundResponse(req, $"Order '{orderId}' not found.");
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                string json = JsonSerializer.Serialize(updated, _jsonOptions);
                await response.WriteStringAsync(json, Encoding.UTF8);
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                return response;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Invalid JSON in status update request.");
                return await CreateBadResponse(req, "Invalid JSON format.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status for order {OrderId}.", orderId);
                return await CreateErrorResponse(req);
            }
        }

        /// <summary>
        /// PUT /api/orders/status - Moves an order one step forward in the lifecycle.
        /// Request body must contain: { "orderId": "ORD-2026-8801" }.
        ///
        /// Status progression:
        ///   Received  → Preparing
        ///   Preparing → Ready
        ///   Ready     → Collected
        ///   Collected → (no further transitions - returns an error)
        /// </summary>
        [Function("AdvanceOrderStatus")]
        public async Task<HttpResponseData> AdvanceOrderStatus(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "orders/status")] HttpRequestData req)
        {
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var body = JsonSerializer.Deserialize<AdvanceStatusRequest>(requestBody, _jsonOptions);

                if (body == null || string.IsNullOrWhiteSpace(body.OrderId))
                {
                    return await CreateBadResponse(req, "orderId field is required.");
                }

                // Fetch the current order
                var existing = await _orderStorage.GetOrderByIdAsync(body.OrderId);
                if (existing == null)
                {
                    return await CreateNotFoundResponse(req, $"Order '{body.OrderId}' not found.");
                }

                // Determine the current status
                if (!Enum.TryParse<OrderStatus>(existing.Status, ignoreCase: true, out var currentStatus))
                {
                    return await CreateBadResponse(req,
                        $"Order has an unknown status '{existing.Status}'.");
                }

                if (currentStatus == OrderStatus.Collected)
                {
                    return await CreateBadResponse(req,
                        $"Order '{body.OrderId}' is already Collected and cannot be advanced further.");
                }

                // Move one step forward
                OrderStatus nextStatus = currentStatus switch
                {
                    OrderStatus.Received => OrderStatus.Preparing,
                    OrderStatus.Preparing => OrderStatus.Ready,
                    OrderStatus.Ready => OrderStatus.Collected,
                    _ => currentStatus
                };

                var updated = await _orderStorage.UpdateOrderStatusAsync(body.OrderId, nextStatus);
                if (updated == null)
                {
                    return await CreateNotFoundResponse(req, $"Order '{body.OrderId}' not found.");
                }

                _logger.LogInformation(
                    "Order {OrderId} advanced from {OldStatus} to {NewStatus}.",
                    body.OrderId, currentStatus, nextStatus);

                var response = req.CreateResponse(HttpStatusCode.OK);
                string json = JsonSerializer.Serialize(updated, _jsonOptions);
                await response.WriteStringAsync(json, Encoding.UTF8);
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                return response;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Invalid JSON in advance status request.");
                return await CreateBadResponse(req, "Invalid JSON format.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error advancing status for order.");
                return await CreateErrorResponse(req);
            }
        }

        // ==== Helper response methods ====

        private async Task<HttpResponseData> CreateBadResponse(HttpRequestData req, string message)
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            string json = JsonSerializer.Serialize(new { error = message }, _jsonOptions);
            await response.WriteStringAsync(json, Encoding.UTF8);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            return response;
        }

        private async Task<HttpResponseData> CreateNotFoundResponse(HttpRequestData req, string message)
        {
            var response = req.CreateResponse(HttpStatusCode.NotFound);
            string json = JsonSerializer.Serialize(new { error = message }, _jsonOptions);
            await response.WriteStringAsync(json, Encoding.UTF8);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            return response;
        }

        private async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            string json = JsonSerializer.Serialize(
                new { error = "An unexpected error occurred. Please try again." }, _jsonOptions);
            await response.WriteStringAsync(json, Encoding.UTF8);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            return response;
        }
    }

    /// <summary>
    /// Request body for updating an order's status to a specific value.
    /// </summary>
    public class UpdateStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request body for advancing an order one step forward in its lifecycle.
    /// </summary>
    public class AdvanceStatusRequest
    {
        public string OrderId { get; set; } = string.Empty;
    }
}