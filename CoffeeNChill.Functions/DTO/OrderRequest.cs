namespace CoffeeNChill.Functions.DTO;

public class OrderRequest
{
    public string CustomerName { get; set; } = string.Empty;
    public List<string> SelectedItemSkUs { get; set; } = new();
    public decimal TotalPrice { get; set; }
}