using System.Collections.Generic;

namespace CoffeeNChill.Functions.Models
{
    /// <summary>
    /// Data Transfer Object representing an incoming order from the queue.
    /// This is the shape of the JSON produced by the Order Queue Producer API.
    /// </summary>
    public class OrderRequest
    {
        /// <summary>
        /// Name of the customer placing the order.
        /// </summary>
        public string CustomerName { get; set; } = string.Empty;

        /// <summary>
        /// Customer's contact number for order updates.
        /// </summary>
        public string CustomerPhone { get; set; } = string.Empty;

        /// <summary>
        /// List of items in the order.
        /// </summary>
        public List<OrderItem> Items { get; set; } = new();

        /// <summary>
        /// Total price of the order.
        /// </summary>
        public double TotalPrice { get; set; }

        /// <summary>
        /// Optional notes from the customer.
        /// </summary>
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Represents a single item in an order.
    /// </summary>
    public class OrderItem
    {
        /// <summary>
        /// Name of the menu item (e.g., "Hot Coffee").
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Quantity ordered.
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Price per unit.
        /// </summary>
        public double Price { get; set; }
    }
}