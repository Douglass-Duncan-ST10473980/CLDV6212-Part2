// this code was written by Neha Heeralal ST10478910
namespace CoffeeNChill.Functions.Models
{
    /// <summary>
    /// Defines the lifecycle stages of an order.
    /// Orders progress through these states in order: Received → Preparing → Ready → Collected.
    /// </summary>
    public enum OrderStatus
    {
        /// <summary>
        /// The order has been received from the queue and stored in the database.
        /// This is the initial state for every new order.
        /// </summary>
        Received,

        /// <summary>
        /// Kitchen staff are actively preparing the order.
        /// </summary>
        Preparing,

        /// <summary>
        /// The order is finished and waiting for the customer to collect it.
        /// </summary>
        Ready,

        /// <summary>
        /// The customer has picked up the order. This is the final state.
        /// </summary>
        Collected
    }
}