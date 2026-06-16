using System.ComponentModel.DataAnnotations;

namespace BlazorForm.Demo.Forms;

public enum AccountKind { Personal, Business }
public enum ContactMethod { Email, Phone, Mail }

/// <summary>Model for the "from C# type" + conditional visibility demo.</summary>
public class AccountSignup
{
    [Required, Display(Name = "Full name"), StringLength(60, MinimumLength = 2)]
    public string FullName { get; set; } = "";

    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Display(Name = "Account type")]
    public AccountKind AccountKind { get; set; }

    [Display(Name = "Company name")]
    public string? CompanyName { get; set; }

    [Display(Name = "VAT number")]
    public string? VatNumber { get; set; }

    [Display(Name = "I accept the terms and conditions")]
    public bool AcceptTerms { get; set; }
}

/// <summary>Model for the array/repeater demo: an invoice with line items.</summary>
public class Invoice
{
    [Required, Display(Name = "Customer")]
    public string Customer { get; set; } = "";

    public List<InvoiceLine> Lines { get; set; } = new();
}

public class InvoiceLine
{
    [Required]
    public string Description { get; set; } = "";

    [Range(1, 9999)]
    public int Quantity { get; set; } = 1;

    [Range(0, 1_000_000), Display(Name = "Unit price")]
    public decimal UnitPrice { get; set; }
}

/// <summary>Model for the FluentValidation demo.</summary>
public class JobApplication
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public int YearsExperience { get; set; }
    [Display(Name = "Desired salary")]
    public int DesiredSalary { get; set; }
    [Display(Name = "Why do you want this role?")]
    public string Motivation { get; set; } = "";
}
