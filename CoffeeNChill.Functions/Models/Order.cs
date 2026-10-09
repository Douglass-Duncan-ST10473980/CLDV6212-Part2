namespace CoffeeNChill.Functions.Models;

/// <summary>
/// Represents an order request submitted by a customer and sent to the
/// <c>order-processing-queue</c> for asynchronous processing.
/// </summary>
/// <remarks>
/// Owner: Douglass Duncan — ST10473980
/// </remarks>
public class Order
{
    /// <summary>
    /// Gets or sets the unique identifier assigned to the order.
    /// </summary>
    /// <example>ORD-2026-8801</example>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full name of the customer placing the order.
    /// </summary>
    /// <example>Jane Smith</example>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the collection of menu-item SKUs selected by the customer.
    /// </summary>
    /// <example>COF-001, PAS-104</example>
    public List<string> SelectedItemSKUs { get; set; } = new();

    /// <summary>
    /// Gets or sets the total price of the order.
    /// </summary>
    /// <example>65.00</example>
    public decimal TotalPrice { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time when the order was submitted.
    /// </summary>
    /// <example>2026-10-12T10:15:00Z</example>
    public DateTimeOffset OrderTimestamp { get; set; }
}