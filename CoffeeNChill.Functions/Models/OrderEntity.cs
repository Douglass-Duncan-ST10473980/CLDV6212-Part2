using Azure;
using Azure.Data.Tables;
/// wirtten by st10478910
namespace CoffeeNChill.Functions.Models
{
    /// <summary>
    /// Represents a customer order stored in the Azure Table Storage "Orders" table.
    /// Uses a composite key: PartitionKey = OrderDate (yyyy-MM-dd) and RowKey = OrderId.
    /// Tracks the full lifecycle from Received → Preparing → Ready → Collected.
    ///
    /// NOTE: This entity is different from the queue message (Douglas's Order class).
    /// The queue message is what arrives; this is what gets persisted.
    /// </summary>
    public class OrderEntity : ITableEntity
    {
        /// <summary>
        /// Partition key = the order date in "yyyy-MM-dd" format (e.g., "2026-10-09").
        /// Groups all orders placed on the same day together.
        /// </summary>
        public string PartitionKey { get; set; } = string.Empty;

        /// <summary>
        /// Unique order identifier. Serves as the RowKey in Table Storage.
        /// Mirrors the OrderId sent by the producer (e.g., "ORD-2026-8801").
        /// </summary>
        public string RowKey { get; set; } = string.Empty;

        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        /// <summary>
        /// Human-readable order ID (mirrors RowKey).
        /// </summary>
        public string OrderId { get; set; } = string.Empty;

        /// <summary>
        /// Name of the customer who placed the order.
        /// </summary>
        public string CustomerName { get; set; } = string.Empty;

        /// <summary>
        /// Comma-separated list of selected item SKUs (e.g., "COF-001,PAS-104").
        /// Stored as a string because Table Storage does not support collections.
        /// </summary>
        public string SelectedItemSKUs { get; set; } = string.Empty;

        /// <summary>
        /// Total price of the order.
        /// </summary>
        public double TotalPrice { get; set; }
        /// <summary>
        /// Current status of the order: Received, Preparing, Ready, or Collected.
        /// </summary>
        public string Status { get; set; } = "Received";

        /// <summary>
        /// UTC timestamp when the order was submitted by the customer
        /// (comes from the queue message).
        /// </summary>
        public DateTimeOffset OrderTimestamp { get; set; }

        /// <summary>
        /// Timestamp when the order was first received by the processor.
        /// </summary>
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp of the most recent status update.
        /// </summary>
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    }
}