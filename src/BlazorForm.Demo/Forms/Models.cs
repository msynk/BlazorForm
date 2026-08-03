using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components.Forms;

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

    /// <summary>Derived by the schema from <see cref="Lines"/>, not typed by the user.</summary>
    [Display(Name = "Invoice total")]
    public decimal Total { get; set; }
}

public class InvoiceLine
{
    [Required]
    public string Description { get; set; } = "";

    [Range(1, 9999)]
    public int Quantity { get; set; } = 1;

    [Range(0, 1_000_000), Display(Name = "Unit price")]
    public decimal UnitPrice { get; set; }

    /// <summary>Derived from <see cref="Quantity"/> and <see cref="UnitPrice"/>.</summary>
    [Display(Name = "Line total")]
    public decimal LineTotal { get; set; }
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

/// <summary>Days a candidate is available. A [Flags] enum renders as a multi-select.</summary>
[Flags]
public enum Weekdays
{
    None = 0,
    [Display(Name = "Mon")] Monday = 1,
    [Display(Name = "Tue")] Tuesday = 2,
    [Display(Name = "Wed")] Wednesday = 4,
    [Display(Name = "Thu")] Thursday = 8,
    [Display(Name = "Fri")] Friday = 16
}

/// <summary>Model for the file upload demo.</summary>
public class DocumentUpload
{
    [Required, Display(Name = "Applicant")]
    public string Applicant { get; set; } = "";

    [Display(Name = "CV (PDF or Word, max 2 MB)")]
    public IBrowserFile? Resume { get; set; }

    [Display(Name = "Supporting documents")]
    public List<IBrowserFile> Attachments { get; set; } = new();
}

/// <summary>Model for the cascading-options demo.</summary>
public class DeliveryAddress
{
    [Required] public string Country { get; set; } = "";
    [Required] public string City { get; set; } = "";
    [Display(Name = "Delivery notes")] public string? Notes { get; set; }
    [Display(Name = "Business delivery")] public bool IsBusiness { get; set; }
    [Display(Name = "Company name")] public string? CompanyName { get; set; }
}

/// <summary>Model for the multi-column layout demo.</summary>
public class Profile
{
    [Required, Display(Name = "First name")] public string FirstName { get; set; } = "";
    [Required, Display(Name = "Last name")] public string LastName { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Phone] public string? Phone { get; set; }
    [Display(Name = "Available on")] public Weekdays Availability { get; set; }
    public PostalAddress Address { get; set; } = new();
    public string? Bio { get; set; }
}

public class PostalAddress
{
    [Required] public string Street { get; set; } = "";
    [Required] public string City { get; set; } = "";
    [Required, Display(Name = "Post code")] public string PostCode { get; set; } = "";
    [Required] public string Country { get; set; } = "";
}

/// <summary>Model for the validation-modes demo. Uses [Compare] for the confirm-password rule.</summary>
public class NewAccount
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, DataType(DataType.Password), MinLength(8)]
    public string Password { get; set; } = "";

    [Required, DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The two passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";

    [Display(Name = "Username")]
    public string Username { get; set; } = "";
}

/// <summary>Model for the field-polish demo: affixes, counters, live updates and static sections.</summary>
public class ListingDraft
{
    [Display(Name = "Listing title")]
    public string Title { get; set; } = "";

    [Display(Name = "Summary")]
    public string Summary { get; set; } = "";

    [Display(Name = "Monthly price")]
    public decimal Price { get; set; }

    [Display(Name = "Floor area")]
    public int FloorArea { get; set; }

    [Display(Name = "City")]
    public string City { get; set; } = "";

    [Display(Name = "Published")]
    public bool Published { get; set; }

    [Display(Name = "Edit passcode")]
    public string Passcode { get; set; } = "";
}

/// <summary>Model for the per-row conditions demo: each contact row asks for a different field.</summary>
public class ContactSheet
{
    [Required, Display(Name = "Sheet name")]
    public string Name { get; set; } = "";

    public List<ContactEntry> Contacts { get; set; } = new();
}

public class ContactEntry
{
    [Display(Name = "Reach them by")]
    public string Kind { get; set; } = "email";

    [Display(Name = "Email address")]
    public string? Email { get; set; }

    [Display(Name = "Phone number")]
    public string? Phone { get; set; }

    [Display(Name = "Postal address")]
    public string? Address { get; set; }
}

/// <summary>
/// Model for the composition demo: named groups, a page-owned layout, per-field undo and an options
/// provider that is allowed to fail.
/// </summary>
public class SupportTicket
{
    [Required, Display(Name = "Your name", GroupName = "Who you are")]
    public string ReporterName { get; set; } = "";

    [Required, EmailAddress, Display(Name = "Email", GroupName = "Who you are")]
    public string ReporterEmail { get; set; } = "";

    [Display(Name = "Team", GroupName = "Where it belongs")]
    public string? Team { get; set; }

    [Display(Name = "Component", GroupName = "Where it belongs")]
    public string? Component { get; set; }

    [Display(Name = "Related ticket")]
    public string? RelatedTicket { get; set; }

    [Required, Display(Name = "Subject")]
    public string Subject { get; set; } = "";

    [Display(Name = "What happened?")]
    public string Details { get; set; } = "";
}

/// <summary>Stand-in for a repository the demo's async rules and option providers call.</summary>
public sealed class DemoDirectory
{
    private static readonly string[] TakenUsernames = ["admin", "root", "ada", "grace"];

    private static readonly Dictionary<string, string[]> CitiesByCountry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fr"] = ["Paris", "Lyon", "Marseille"],
        ["gb"] = ["London", "Manchester", "Bristol"],
        ["de"] = ["Berlin", "Hamburg", "Munich"],
        ["jp"] = ["Tokyo", "Osaka", "Kyoto"]
    };

    public static IReadOnlyList<BlazorFormSelectOption> Countries =>
    [
        new("fr", "France"), new("gb", "United Kingdom"), new("de", "Germany"), new("jp", "Japan")
    ];

    /// <summary>Simulates the latency of a real lookup so the loading state is visible.</summary>
    public async ValueTask<IEnumerable<string>> GetCitiesAsync(string? countryCode, CancellationToken ct = default)
    {
        await Task.Delay(400, ct);
        return countryCode is not null && CitiesByCountry.TryGetValue(countryCode, out var cities)
            ? cities
            : [];
    }

    public async ValueTask<bool> IsUsernameAvailableAsync(string username, CancellationToken ct = default)
    {
        await Task.Delay(400, ct);
        return !TakenUsernames.Contains(username, StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<BlazorFormSelectOption> Teams =>
    [
        new("platform", "Platform"), new("billing", "Billing"), new("mobile", "Mobile")
    ];

    /// <summary>
    /// A lookup that fails on demand, so the composition demo can show what a form does when the
    /// service behind a dropdown is down — which is the interesting case, and the one nobody demos.
    /// </summary>
    public async ValueTask<IEnumerable<string>> GetComponentsAsync(string? team, bool fail, CancellationToken ct = default)
    {
        await Task.Delay(300, ct);
        if (fail) throw new InvalidOperationException("The component service is unavailable.");

        return team switch
        {
            "platform" => ["API", "Scheduler", "Auth"],
            "billing" => ["Invoicing", "Payments"],
            "mobile" => ["iOS app", "Android app"],
            _ => []
        };
    }
}
