using System;
using System.Collections.Generic;
/// code was written by st10478910 
namespace CoffeeNChill.Functions.Models
{
    /// <summary>
    /// Data Transfer Object representing an incoming order message from the
    /// "order-processing-queue". Matches the shape of the JSON produced by the
    /// Order Queue Producer (Douglas's Order class).
    ///
    /// Field names in JSON are camelCase (orderId, customerName, selectedItemSKUs, ...).
    /// These are read correctly by setting PropertyNameCaseInsensitive = true
    /// in the JsonSerializerOptions used by OrderQueueFunctions.
    /// </summary>
    public class OrderMessage
    {
        /// <summary>
        /// Unique identifier assigned to the order by the producer.
        /// Example: "ORD-2026-8801"
        /// </summary>
        public string OrderId { get; set; } = string.Empty;

        /// <summary>
        /// Full name of the customer placing the order.
        /// </summary>
        public string CustomerName { get; set; } = string.Empty;

        /// <summary>
        /// Collection of menu-item SKUs selected by the customer.
        /// Example: ["COF-001", "PAS-104"]
        /// </summary>
        public List<string> SelectedItemSKUs { get; set; } = new();

        /// <summary>
        /// Total price of the order.
        /// </summary>
        public decimal TotalPrice { get; set; }

        /// <summary>
        /// UTC timestamp when the order was submitted.
        /// </summary>
        public DateTimeOffset OrderTimestamp { get; set; }
    }
}