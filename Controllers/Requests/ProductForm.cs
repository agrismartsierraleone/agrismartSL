namespace AgrismartAPI.Controllers.Requests;

public class ProductForm
{
    public required string Name { get; set; }
    public required Guid CategoryId { get; set; }
    public required decimal Price { get; set; }
    public required string Unit { get; set; }
    public required Guid SellerId { get; set; }
    public required string Description { get; set; }
    public IFormFile? Image { get; set; }
}
