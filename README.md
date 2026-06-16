# BlazorForm

Schema-driven form engine for Blazor. Render forms from C# types or JSON Schema, with validation, conditional visibility, multi-step wizards, and array/repeater fields. The renderers are dependency-light HTML with a pluggable renderer registry, so you can swap in your own widgets.

## Features

- **Schema-first** – describe a form once as a `FormDefinition` and render it anywhere.
- **Multiple sources** – generate a schema from a POCO via reflection + DataAnnotations, build one with a fluent API, or import a JSON Schema document.
- **Strongly-typed builder** – select fields with lambda expressions so names and value types are inferred and refactor-safe.
- **Rich field types** – text, number, select, multi-select, radio, checkbox, date/time, range, color, file, nested objects, and repeating arrays.
- **Validation** – built-in rules (required, length, range, pattern, email, collection size), custom sync/async rules, and optional [FluentValidation](https://docs.fluentvalidation.net/) integration.
- **Conditional behaviour** – show/hide or disable fields and wizard steps based on other field values.
- **Wizards** – split a form into ordered steps with per-step validation.
- **Pluggable renderers** – override the default HTML inputs through a renderer registry.

## Requirements

- .NET 10.0
- `Microsoft.AspNetCore.Components.Web` 10.0.9
- `FluentValidation` 12.1.1 (used by the optional integration)

## Installation

```bash
dotnet add package BlazorForm
```

Or reference the project directly:

```xml
<ProjectReference Include="..\BlazorForm\BlazorForm.csproj" />
```

## Quick start

### 1. From a C# model

Annotate a plain model and let the schema generator do the work:

```csharp
using System.ComponentModel.DataAnnotations;

public class Contact
{
    [Required, MaxLength(80)]
    public string Name { get; set; } = "";

    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Range(18, 120)]
    public int Age { get; set; }
}
```

```csharp
@using BlazorForm.Core.Generation
@using BlazorForm.Core.Schema

@code {
    private FormDefinition _definition = SchemaGenerator.Generate<Contact>();
    private Contact _model = new();

    private Task HandleValidSubmit(FormState state)
    {
        // _model is populated and valid here
        return Task.CompletedTask;
    }
}

<BlazorFormView Definition="_definition"
                Model="_model"
                OnValidSubmit="HandleValidSubmit" />
```

### 2. With the fluent builder

```csharp
using BlazorForm.Core.Building;
using BlazorForm.Core.Schema;

var definition = FormBuilder.For<Contact>()
    .Title("Contact us")
    .Field(x => x.Name, f => f.Required().Placeholder("Your name"))
    .Field(x => x.Email, f => f.AsEmail().Required())
    .Field(x => x.Age, f => f.Range(18, 120))
    .Build();
```

The untyped builder is handy when you have no compiled model:

```csharp
var definition = FormBuilder.Create()
    .Title("Feedback")
    .Text("subject", f => f.Required())
    .Field("body", FieldType.TextArea, f => f.AsTextArea(rows: 6))
    .Select("topic", f => f.Options(("bug", "Bug"), ("idea", "Idea")))
    .Build();
```

### 3. From a JSON Schema

```csharp
using BlazorForm.Core.Json;

var definition = JsonSchemaImporter.Import(jsonSchemaString);
```

The importer supports common draft-07 keywords (`type`, `properties`, `required`, `enum`,
`format`, `minimum`/`maximum`, `minLength`/`maxLength`, `pattern`, `items`) plus a few `x-`
extensions for UI intent: `x-widget`, `x-order`, `x-placeholder`, and `enumNames`.

## Validation

Built-in rules are added through the field builder:

```csharp
.Field(x => x.Username, f => f
    .Required("Username is required")
    .MinLength(3)
    .MaxLength(20)
    .Pattern("^[a-z0-9_]+$", "Lowercase letters, numbers and underscores only"))
```

Custom rules:

```csharp
.Field(x => x.Password, f => f
    .AsPassword()
    .Must(value => value is string s && s.Length >= 8, "At least 8 characters"))
```

### FluentValidation

Wire up a FluentValidation validator on the form state. Failure property paths
(e.g. `Address.City`, `Items[0].Product`) map directly onto field paths.

```csharp
using BlazorForm.FluentValidation;

var state = new FormState(definition, new ModelDataAccessor(model), serviceProvider)
    .UseFluentValidation(new ContactValidator());

// Pass the pre-configured state to the view
```

```razor
<BlazorFormView State="state" OnValidSubmit="HandleValidSubmit" />
```

If your validators are registered in DI, call `state.UseFluentValidation()` (no argument) and the
matching `IValidator<TModel>` is resolved from the service provider.

## Conditional fields and wizards

Show or disable fields based on other values:

```csharp
.Field(x => x.CompanyName, f => f
    .VisibleWhen(nameof(Model.IsBusiness), ConditionOperator.Equals, true))
```

Split a form into steps:

```csharp
FormBuilder.For<Order>()
    .Step("details", s => s.Title("Details").Fields("Name", "Email"))
    .Step("shipping", s => s.Title("Shipping").Fields("Address", "City", "Zip"))
    .Build();
```

`BlazorFormView` renders a stepper, Back/Next navigation, and validates each step before advancing.

## The `BlazorFormView` component

| Parameter | Description |
| --- | --- |
| `Definition` | The schema to render. Required unless `State` is supplied. |
| `Model` | Optional typed model to bind to. When omitted, a dictionary store is used. |
| `Data` | Optional `IDictionary<string, object?>` backing store (used when `Model` is null). |
| `State` | Provide a pre-configured `FormState` (e.g. with FluentValidation wired up). |
| `OnValidSubmit` | Raised with the state after a successful (valid) submit. |
| `OnInvalidSubmit` | Raised after a submit that failed validation. |
| `ShowSubmitButton` | Whether to render the built-in submit button (default `true`). |
| `SubmitText` | Text for the submit button (default `"Submit"`). |
| `ChildContent` | Extra content rendered inside the `<form>`. |

## Project layout

```
src/BlazorForm
├── Building/      Fluent form & field builders
├── Components/    Razor components (BlazorFormView, field & input views)
├── Data/          Data accessors (model & dictionary) and form paths
├── Generation/    Reflection + DataAnnotations schema generation
├── Json/          JSON Schema import/export
└── Schema/        Field types and schema model
```

## License

Licensed under the [MIT License](LICENSE).
