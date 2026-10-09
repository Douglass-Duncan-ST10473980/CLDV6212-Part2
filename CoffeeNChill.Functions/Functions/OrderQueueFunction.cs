using System.Text.Json;
using CoffeeNChill.Functions.Services;
using Microsoft.Extensions.Logging;

namespace CoffeeNChill.Functions.Functions;

public class OrderQueueFunction
{
    
    private readonly ILogger<OrderQueueFunction> _logger;
    private readonly OrderQueueService _orderQueueService;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}