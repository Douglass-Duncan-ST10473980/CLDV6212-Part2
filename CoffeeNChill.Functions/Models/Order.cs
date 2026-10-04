using Azure;
using Azure.Data.Tables;
// this code was written by Neha Heeralal ST10478910
namespace CoffeeNChill.Functions.Models
{
    /// <summary>
    /// Represents a customer order stored in the Azure Table Storage "Orders" table.
    /// Uses a composite key: PartitionKey = "Order" (fixed) and RowKey = unique OrderId.
    /// Tracks the full lifecycle from Received → Preparing → Ready → Collected.
    /// </summary>
    public class Order : ITableEntity
    {
        /// <summary>
        /// Fixed partition key for all orders. Using a single partition keeps all orders
        /// in the same logical group, which works well for our low-volume scenario.
        /// </summary>
        public string PartitionKey { get; set; } = "Order";

        /// <summary>
        /// Unique order identifier (GUID). Serves as the RowKey in Table Storage.
        /// </summary>
        public string RowKey { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Azure Table Storage timestamp (managed automatically by the service).
        /// </summary>
        public DateTimeOffset? Timestamp { get; set; }

        /// <summary>
        /// Azure Table Storage ETag used for optimistic concurrency.
        /// </summary>
        public ETag ETag { get; set; }

        /// <summary>
        /// Human-friendly order ID shown to staff and customers (e.g., "ORD-20261004-001").
        /// </summary>
        public string OrderId { get; set; } = string.Empty;

        /// <summary>
        /// Name of the customer who placed the order.
        /// </summary>
        public string CustomerName { get; set; } = string.Empty;

        /// <summary>
        /// Customer's contact number for order updates.
        /// </summary>
        public string CustomerPhone { get; set; } = string.Empty;

        /// <summary>
        /// JSON-serialized array of items in the order (name, quantity, price).
        /// Stored as a string because Table Storage does not support nested objects.
        /// </summary>
        public string ItemsJson { get; set; } = string.Empty;

        /// <summary>
        /// Total price of the order in the local currency.
        /// </summary>
        public double TotalPrice { get; set; }

        /// <summary>
        /// Current status of the order: Received, Preparing, Ready, or Collected.
        /// </summary>
        public string Status { get; set; } = "Received";

        /// <summary>
        /// Any special notes the customer included with the order.
        /// </summary>
        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the order was first received into the system.
        /// </summary>
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp of the most recent status update.
        /// </summary>
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    }
}