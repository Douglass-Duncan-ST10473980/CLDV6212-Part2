namespace CoffeeNChill.Functions.Models;

public class Order
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public List<string> SelectedItemSkUs { get; set; } = new();
    public decimal TotalPrice { get; set; }
    public DateTime OrderTimeStamp { get; set; }
}