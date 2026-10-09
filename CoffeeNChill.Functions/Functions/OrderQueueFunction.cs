using System.Net;
using System.Text;
using System.Text.Json;
using CoffeeNChill.Functions.DTO;
using CoffeeNChill.Functions.Models;
using CoffeeNChill.Functions.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace CoffeeNChill.Functions.Functions;

public class OrderQueueFunction
{
    
    private readonly ILogger<OrderQueueFunction> _logger;
    private readonly OrderQueueService _orderQueueService;
    private readonly MenuStorageService  _menuStorageService;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Function("QueueOrder")]
    public async Task<HttpResponseData> QueueOrder(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "orders/queue")]
        HttpRequestData req)
    {
        
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        OrderRequest? request = JsonSerializer.Deserialize<OrderRequest>(requestBody, _jsonOptions);

        if (request == null)
        {
            return await CreateBadResponse(req, "Invalid request body.");
        }

        if (string.IsNullOrEmpty(request.CustomerName))
        {
            return await CreateBadResponse(req, "CustomerName is required.");
        }

        if (request.CustomerName.Length < 3)
        {
            return await CreateBadResponse(req, "CustomerName is too short.");
        }

        if (request.CustomerName.Length > 40)
        {
            return await CreateBadResponse(req, "CustomerName is too long.");
        }
        
        decimal totalPrice = 0;
        
        foreach (string sku in request.SelectedItemSKUs)
        {
            MenuItem? menuItem = await _menuStorageService.GetMenuItemBySkuAsync(sku);

            if (menuItem is null)
            {
                var response = req.CreateResponse(HttpStatusCode.BadRequest);

                await response.WriteAsJsonAsync(new
                {
                    message = $"Menu item with SKU '{sku}' was not found."
                });

                return response;
            }

            
        }

        if (totalPrice <= 0)
        {
            return await CreateBadResponse(req, "Price must be greater than or equal to zero.");
        }
        
        var order = new Order
        {
            OrderId = $"ORD-{DateTime.UtcNow.Year}-{Random.Shared.Next(1000, 10000)}",
            CustomerName = request.CustomerName,
            SelectedItemSKUs = request.SelectedItemSKUs,
            TotalPrice = totalPrice,
            OrderTimestamp = DateTimeOffset.UtcNow
        };
        
    }
    
    private async Task<HttpResponseData> CreateBadResponse(HttpRequestData req, string message)
    {
        var response = req.CreateResponse(HttpStatusCode.BadRequest);
        string json = JsonSerializer.Serialize(new { error = message }, _jsonOptions);
        await response.WriteStringAsync(json, Encoding.UTF8);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        return response;
    }
}