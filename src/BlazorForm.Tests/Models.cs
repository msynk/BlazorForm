using System.ComponentModel.DataAnnotations;

namespace BlazorForm.Tests;

public enum AccountType { Personal, Business }

public class Address
{
    [Required] public string Street { get; set; } = "";
    [Required] public string City { get; set; } = "";
    [RegularExpression(@"^\d{5}$")] public string Zip { get; set; } = "";
}

public class LineItem
{
    [Required] public string Product { get; set; } = "";
    [Range(1, 100)] public int Quantity { get; set; }
}

public class RegistrationModel
{
    [Required, Display(Name = "First name")]
    [StringLength(50, MinimumLength = 2)]
    public string FirstName { get; set; } = "";

    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Range(18, 120)]
    public int Age { get; set; }

    public AccountType AccountType { get; set; }

    public string? CompanyName { get; set; }

    public Address Address { get; set; } = new();

    public List<LineItem> Items { get; set; } = new();
}
