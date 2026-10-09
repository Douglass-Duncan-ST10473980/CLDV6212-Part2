using System.Net;
using System.Text.Json;
using Azure;
using CoffeeNChill.Functions.DTO;
using CoffeeNChill.Functions.Models;
using CoffeeNChill.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace CoffeeNChill.Functions.Functions;

/// <summary>
/// Provides the HTTP endpoint used to validate customer orders and place them
/// onto the order-processing queue.
/// </summary>
/// <remarks>
/// Owner: Douglass Duncan — ST10473980
/// </remarks>
public class OrderQueueFunction
{
    private readonly ILogger<OrderQueueFunction> _logger;
    private readonly OrderQueueService _orderQueueService;
    private readonly MenuStorageService _menuStorageService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderQueueFunction"/> class.
    /// </summary>
    /// <param name="menuStorageService">
    /// The service used to retrieve menu-item information.
    /// </param>
    /// <param name="logger">
    /// The logger used to record order submission information and errors.
    /// </param>
    /// <param name="orderQueueService">
    /// The service used to submit completed orders to Azure Queue Storage.
    /// </param>
    public OrderQueueFunction(
        MenuStorageService menuStorageService,
        ILogger<OrderQueueFunction> logger,
        OrderQueueService orderQueueService)
    {
        _menuStorageService = menuStorageService;
        _logger = logger;
        _orderQueueService = orderQueueService;
    }

    /// <summary>
    /// Validates an incoming order, calculates its total using prices from the
    /// MenuItems table and places the completed order onto the
    /// order-processing queue.
    /// </summary>
    /// <param name="req">The incoming HTTP request.</param>
    /// <param name="cancellationToken">
    /// A token used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <c>202 Accepted</c> response when the order is queued successfully;
    /// otherwise, an appropriate error response.
    /// </returns>
    [Function("QueueOrder")]
    public async Task<HttpResponseData> QueueOrder(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "orders/queue")]
        HttpRequestData req,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received a request to submit an order for queue processing.");

        try
        {
            OrderRequest? orderRequest =
                await JsonSerializer.DeserializeAsync<OrderRequest>(
                    req.Body,
                    JsonOptions,
                    cancellationToken);

            if (orderRequest is null)
            {
                _logger.LogWarning(
                    "Order submission rejected because the request body was empty.");

                return await CreateErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "Invalid or empty request body.",
                    cancellationToken);
            }

            string customerName = orderRequest.CustomerName.Trim();

            if (string.IsNullOrWhiteSpace(customerName))
            {
                _logger.LogWarning(
                    "Order submission rejected because CustomerName was missing.");

                return await CreateErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "CustomerName is required.",
                    cancellationToken);
            }

            if (customerName.Length < 3)
            {
                _logger.LogWarning(
                    "Order submission rejected because CustomerName was too short.");

                return await CreateErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "CustomerName must contain at least 3 characters.",
                    cancellationToken);
            }

            if (customerName.Length > 40)
            {
                _logger.LogWarning(
                    "Order submission rejected because CustomerName was too long.");

                return await CreateErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "CustomerName cannot contain more than 40 characters.",
                    cancellationToken);
            }

            if (orderRequest.SelectedItemSKUs.Count == 0)
            {
                _logger.LogWarning(
                    "Order submission rejected because no menu-item SKUs were supplied.");

                return await CreateErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "At least one menu-item SKU is required.",
                    cancellationToken);
            }

            if (orderRequest.SelectedItemSKUs.Any(
                    sku => string.IsNullOrWhiteSpace(sku)))
            {
                _logger.LogWarning(
                    "Order submission rejected because one or more SKUs were empty.");

                return await CreateErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "SelectedItemSKUs cannot contain empty values.",
                    cancellationToken);
            }

            List<string> normalizedSkus = orderRequest.SelectedItemSKUs
                .Select(sku => sku.Trim().ToUpperInvariant())
                .ToList();

            decimal totalPrice = 0;

            foreach (string sku in normalizedSkus)
            {
                _logger.LogInformation(
                    "Retrieving menu item with SKU {Sku}.",
                    sku);

                MenuItem? menuItem =
                    await _menuStorageService.GetMenuItemBySkuAsync(sku);

                if (menuItem is null)
                {
                    _logger.LogWarning(
                        "Order submission rejected because SKU {Sku} was not found.",
                        sku);

                    return await CreateErrorResponse(
                        req,
                        HttpStatusCode.BadRequest,
                        $"Menu item with SKU '{sku}' was not found.",
                        cancellationToken);
                }

                if (menuItem.Price <= 0)
                {
                    _logger.LogError(
                        "Menu item {Sku} has an invalid price of {Price}.",
                        sku,
                        menuItem.Price);

                    return await CreateErrorResponse(
                        req,
                        HttpStatusCode.InternalServerError,
                        $"Menu item with SKU '{sku}' has an invalid price.",
                        cancellationToken);
                }

                totalPrice += (decimal)menuItem.Price;

                _logger.LogDebug(
                    "Added menu item {Sku} at {Price} to the order total.",
                    sku,
                    menuItem.Price);
            }

            if (totalPrice <= 0)
            {
                _logger.LogError(
                    "Calculated order total was invalid: {TotalPrice}.",
                    totalPrice);

                return await CreateErrorResponse(
                    req,
                    HttpStatusCode.InternalServerError,
                    "The order total could not be calculated.",
                    cancellationToken);
            }

            var order = new Order
            {
                OrderId = GenerateOrderId(),
                CustomerName = customerName,
                SelectedItemSKUs = normalizedSkus,
                TotalPrice = totalPrice,
                OrderTimestamp = DateTimeOffset.UtcNow
            };

            _logger.LogInformation(
                "Submitting order {OrderId} containing {ItemCount} item(s) " +
                "with a total of {TotalPrice} to the order-processing queue.",
                order.OrderId,
                order.SelectedItemSKUs.Count,
                order.TotalPrice);

            await _orderQueueService.EnqueueOrderAsync(
                order,
                cancellationToken);

            _logger.LogInformation(
                "Order {OrderId} was successfully placed onto the queue.",
                order.OrderId);

            var acceptedResponse =
                req.CreateResponse(HttpStatusCode.Accepted);

            await acceptedResponse.WriteAsJsonAsync(
                new
                {
                    message = "Order accepted for processing.",
                    orderId = order.OrderId,
                    status = "Queued",
                    totalPrice = order.TotalPrice,
                    orderTimestamp = order.OrderTimestamp
                },
                cancellationToken);

            return acceptedResponse;
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(
                exception,
                "Order submission contained malformed JSON.");

            return await CreateErrorResponse(
                req,
                HttpStatusCode.BadRequest,
                "The request body contains invalid JSON.",
                cancellationToken);
        }
        catch (RequestFailedException exception)
        {
            _logger.LogError(
                exception,
                "An Azure Storage error occurred while submitting the order. " +
                "Status: {Status}; Error code: {ErrorCode}.",
                exception.Status,
                exception.ErrorCode);

            return await CreateErrorResponse(
                req,
                HttpStatusCode.ServiceUnavailable,
                "The order-processing service is temporarily unavailable.",
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Order submission was cancelled.");

            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "An unexpected error occurred while submitting an order.");

            return await CreateErrorResponse(
                req,
                HttpStatusCode.InternalServerError,
                "An unexpected error occurred while processing the order.",
                cancellationToken);
        }
    }

    /// <summary>
    /// Generates an order identifier containing the current year and a random
    /// four-digit number.
    /// </summary>
    /// <returns>A formatted order identifier.</returns>
    private static string GenerateOrderId()
    {
        return $"ORD-{DateTime.UtcNow.Year}-{Random.Shared.Next(1000, 10000)}";
    }

    /// <summary>
    /// Creates a JSON error response using the supplied HTTP status code.
    /// </summary>
    private static async Task<HttpResponseData> CreateErrorResponse(
        HttpRequestData req,
        HttpStatusCode statusCode,
        string message,
        CancellationToken cancellationToken)
    {
        HttpResponseData response = req.CreateResponse(statusCode);

        await response.WriteAsJsonAsync(
            new
            {
                error = message
            },
            cancellationToken);

        return response;
    }
}