using System.ComponentModel.DataAnnotations;

namespace LawnDart.Samples.EShop.WebApp.Models;

public class BasketCheckoutInfo
{
    [Required]
    public string? Street { get; set; }

    [Required]
    public string? City { get; set; }

    [Required]
    public string? State { get; set; }

    [Required]
    public string? Country { get; set; }

    [Required]
    public string? ZipCode { get; set; }

    public string? CardHolderName { get; set; }

    public DateTime? CardExpiration { get; set; }

    public int CardTypeId { get; set; }

    public string? Buyer { get; set; }

    public Guid RequestId { get; set; }
}
