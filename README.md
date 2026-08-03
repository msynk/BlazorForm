# BlazorForm

Schema-driven form engine for Blazor. Render forms from C# types or JSON Schema, with validation, conditional visibility, multi-step wizards, and array/repeater fields. The renderers are dependency-light HTML with a pluggable renderer registry, so you can swap in your own widgets.

## Features

- **Schema-first** – describe a form once as a `BlazorFormDefinition` and render it anywhere.
- **Multiple sources** – generate a schema from a POCO via reflection + DataAnnotations, build one with a fluent API, or import a JSON Schema document.
- **Strongly-typed builder** – select fields with lambda expressions (including nested ones, `x => x.Address.City`) so names and value types are inferred and refactor-safe.
- **Rich field types** – text, search, number, select, **combobox**, multi-select, radio, checkbox/switch, date/time, range, color, file upload, **tags**, static section headings, nested objects, and repeating arrays.
- **Field polish** – prefix/suffix affixes, live character counters, `<datalist>` suggestions, a password reveal toggle, a clear button, and arbitrary HTML attributes splatted onto any control.
- **Live updates** – write on every keystroke instead of on blur, with optional debouncing, for as-you-type previews and validation.
- **Validation** – built-in rules (required, length, range, pattern, email, URL, compare, multiple-of, unique items, collection size, file size/type), custom sync/async rules, form-level cross-field rules, and optional [FluentValidation](https://docs.fluentvalidation.net/) integration.
- **Dependent revalidation** – `RevalidateOn(...)` runs a field's rules again when the *other* field they read changes, so fixing the password clears the confirmation's error instead of leaving it stranded.
- **Validation modes** – choose when fields revalidate before and after the first submit (`OnSubmit` / `OnBlur` / `OnChange`).
- **Conditional behaviour** – show/hide, disable, or conditionally require fields and wizard steps based on other values, and clear hidden values so abandoned branches never reach your model.
- **Async & cascading options** – load select options from a service and reload them when a dependency changes.
- **Computed values** – derive a field from the rest of the form (totals, full names), including per-row formulas inside a repeater.
- **Change handlers** – `OnChange(...)` puts "when the country changes, clear the city" in the schema, where the rest of the field's behaviour already lives.
- **Wizards** – split a form into ordered steps with per-step validation, conditional steps and a clickable stepper.
- **Accessible by default** – labels, `aria-required` / `aria-invalid` / `aria-describedby`, live-announced errors, grouped radio and multi-select controls, real focus management (autofocus, and focus-the-first-error after a failed submit), and an optional error summary that links to each field.
- **Localisable** – swap *all* the built-in English text — validation messages, buttons, placeholders, repeater labels — for your own via `IBlazorFormMessageProvider`.
- **Schema diagnostics** – `Definition.Validate()` catches duplicate field names, arrays with no item template, steps naming fields that do not exist, conditions pointing nowhere, and settings a control can never honour.
- **Composable layout** – render the whole form with `BlazorFormView`, cluster fields into named
  `<fieldset>` groups, or place them one at a time with `<BlazorFormField>` and own the layout yourself.
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

All public types live in the single `BlazorForm` namespace and are prefixed with `BlazorForm`, so a single `@using BlazorForm` (or `using BlazorForm;`) brings everything into scope.

Add the stylesheet to your host page:

```html
<link rel="stylesheet" href="_content/BlazorForm/blazorform.css" />
```

Every colour is a CSS custom property on `.ff-form`, and the default theme follows the OS light/dark preference.

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
@using BlazorForm

@code {
    private BlazorFormDefinition _definition = BlazorFormSchemaGenerator.Generate<Contact>();
    private Contact _model = new();

    private Task HandleValidSubmit(BlazorFormState state)
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
using BlazorForm;

var definition = BlazorFormBuilder.For<Contact>()
    .Title("Contact us")
    .Field(x => x.Name, f => f.Required().Placeholder("Your name"))
    .Field(x => x.Email, f => f.AsEmail().Required())
    .Field(x => x.Age, f => f.Range(18, 120))
    .Build();
```

Constraints replace, rather than stack on top of, the rules generated from DataAnnotations, so
restating `[Required]` in the builder to give it a better message produces **one** error, not two.

Nested members, object groups and arrays are all expression-based:

```csharp
BlazorFormBuilder.For<Order>()
    .Field(x => x.Customer.Email, f => f.Required())          // binds to "Customer.Email"
    .Object(x => x.ShippingAddress, a => a
        .Field(p => p.Street)
        .Field(p => p.City))
    .Array(x => x.Lines, i => i
        .Field(l => l.Product, f => f.Required())
        .Field(l => l.Quantity, f => f.Range(1, 999)))
    .Build();
```

The untyped builder is handy when you have no compiled model:

```csharp
var definition = BlazorFormBuilder.Create()
    .Title("Feedback")
    .Text("subject", f => f.Required())
    .Field("body", BlazorFormFieldType.TextArea, f => f.AsTextArea(rows: 6))
    .Select("topic", f => f.Options(("bug", "Bug"), ("idea", "Idea")))
    .ArrayOf("tags", BlazorFormFieldType.Text, f => f.Items(max: 5).UniqueItems())
    .Build();
```

### 3. From a JSON Schema

```csharp
using BlazorForm;

var definition = BlazorFormJsonSchemaImporter.Import(jsonSchemaString);
```

The importer covers draft-07 through 2020-12: `type` (including nullable unions such as
`["string","null"]`), `properties`, `required`, `enum`, `const`, `format`, `default`, `examples`,
`readOnly`, `minimum`/`maximum` and their exclusive forms, `multipleOf`, `minLength`/`maxLength`,
`pattern`, `items`, `minItems`/`maxItems`, `uniqueItems`, `$ref` into `$defs`/`definitions`, `allOf`
(merged), `if`/`then`/`else` and `dependentRequired`/`dependencies` — the last two mapped onto
conditional requiredness.

`anyOf`/`oneOf` is handled for the two shapes that describe a single control: a null union
(`{"anyOf":[{"type":"string"},{"type":"null"}]}`) collapses to the branch it wraps, and a list of
`const` branches with titles becomes a labelled select. A union of genuinely different *object* shapes
describes a choice of subforms rather than a choice of values — it needs a selector and a form that
swaps with it — so it is left alone rather than bound to a branch the document never committed to.

Use `TryImport` when the schema comes from a user:

```csharp
if (BlazorFormJsonSchemaImporter.TryImport(json, out var definition, out var error))
    // render it
else
    ShowError(error);
```

UI intent that JSON Schema has no vocabulary for travels in `x-` extensions, and all of it
round-trips through `BlazorFormJsonSchemaExporter.Export(definition)`:

| Extension | Meaning |
| --- | --- |
| `x-widget` | Force a control: `textarea`, `radio`, `combobox`, `multiselect`, `tags`, `range`, `switch`, `file`, `static`, `hidden`, … |
| `x-renderer` | A custom renderer key registered with `RegisterCustom`. |
| `x-order`, `x-group`, `x-placeholder`, `x-autocomplete`, `x-inputMode`, `x-autofocus` | Field metadata. |
| `x-columns`, `x-colSpan` | Grid layout. |
| `x-prefix`, `x-suffix`, `x-showLabel`, `x-characterCount` | Presentation. |
| `x-updateOn`, `x-debounce` | When the value is written back. |
| `x-step` | The control's granularity, as distinct from a `multipleOf` constraint. |
| `x-accept`, `x-multiple`, `x-maxFileSize` | File upload constraints. |
| `x-visibleWhen`, `x-disabledWhen`, `x-requiredWhen` | Conditions, as `{"field":…,"op":…,"value":…}` or `{"all":[…]}` / `{"any":[…]}`. |
| `x-revalidateOn` | Paths whose change revalidates this field — the other half of a cross-field rule. |
| `x-clearOnHide` | Clear the value when the field is hidden. |
| `x-attributes`, `x-inputAttributes` | Renderer hints and extra HTML attributes. |
| `x-steps` | Wizard steps, each with `id`, `title`, `fields` and an optional `visibleWhen`. |
| `enumNames`, `x-enumGroups`, `x-enumDisabled` | Labels matching the `enum` values, the `<optgroup>` each belongs to, and the values that may not be chosen. |
| `examples` | `<datalist>` suggestions. |

Conditions, rules and renderer hints backed by code (`VisibleWhen(predicate)`, `Must`, `MustAsync`, a
delegate stashed in `Attributes`) have no JSON form and are omitted from the export rather than
approximated.

## Validation

Built-in rules are added through the field builder:

```csharp
.Field(x => x.Username, f => f
    .Required("Username is required")
    .MinLength(3)
    .MaxLength(20)
    .Pattern("^[a-z0-9_]+$", "Lowercase letters, numbers and underscores only"))
```

Custom, async and cross-field rules:

```csharp
.Field(x => x.Password, f => f
    .AsPassword()
    .Must(value => value is string s && s.Length >= 8, "At least 8 characters"))

.Field(x => x.ConfirmPassword, f => f
    .AsPassword()
    .MatchesField(nameof(Model.Password), "Password"))

.Field(x => x.Username, f => f
    .MustAsync(async ctx => await users.IsAvailableAsync((string?)ctx.Value ?? ""), "That username is taken."))
```

Async rules are skipped while the user types and run on blur and on submit.

### Rules that read another field

A cross-field rule lives on one of the two fields it compares, so only one of the two changes ever
runs it. Get the confirmation wrong, then fix the *password* to agree with what you typed, and the
complaint under the confirmation box is now false — and stays there until you go back and touch a
field that was already right. `RevalidateOn` closes that:

```csharp
.Field(x => x.End, f => f
    .RevalidateOn(nameof(Booking.Start))
    .Must(ctx => …, "End must be on or after start."))
```

`MatchesField(...)` and `[Compare]` register their own dependency, so a confirm-password field needs
nothing extra. Paths resolve against the object that owns the field before falling back to the root,
exactly as conditions and computed dependencies do, so a rule on a repeater's item template means
*that row*; naming a container covers everything inside it.

A dependent that has nothing to say is left alone — a field the user has never visited on a form
nobody has submitted does not start showing errors because a different field changed. The point is to
correct a verdict already on screen, never to bring one forward. React Hook Form spells this `deps`;
TanStack Form spells it `onChangeListenTo`. `Definition.Validate()` reports a comparison rule that
never revalidates.

Rules can also be scoped to a condition, the way FluentValidation's `.When(…)` works:

```csharp
.Field(x => x.CompanyName, f => f
    .When(nameof(Model.IsBusiness), BlazorFormConditionOperator.IsTrue, null, w => w
        .Required("Business accounts need a company name.")
        .MinLength(3)))
```

Two details worth knowing:

- **`Required()` on a checkbox means "must be ticked"** — the same thing HTML's own `required`
  attribute means, and what "I accept the terms" always needs. On any other field type, `false` is a
  perfectly good value.
- **A value the model cannot accept is reported, not swallowed.** Typing `abc` into a field bound to
  an `int` leaves the model on its last valid value and raises a validation message naming the entry,
  rather than silently discarding it on the next render.

Rules that need the whole model live on the form:

```csharp
BlazorFormBuilder.For<Booking>()
    .Field(x => x.Start)
    .Field(x => x.End)
    .MustAll(m => m.End >= m.Start, "End must be on or after start.", nameof(Booking.End))
    .Build();
```

### When validation runs

```csharp
var state = new BlazorFormState(definition, accessor)
{
    ValidationTrigger   = BlazorFormValidationTrigger.OnBlur,   // before the first submit
    RevalidationTrigger = BlazorFormValidationTrigger.OnChange  // after it
};
```

A field that already shows an error always revalidates eagerly, so the error clears as soon as it is fixed.

### How much is reported at once

By default a field shows every rule it currently breaks. Set `SingleErrorPerField` to show only the
first — React Hook Form's default, and the right call for a password with four constraints on it, where
four complaints under one box read as four problems rather than one field to go and fix:

```csharp
state.SingleErrorPerField = true;
```

Every rule still runs, so the submit decision is unchanged; this is about what the user reads.
Warnings sit alongside the error rather than competing with it.

### Editing an existing record

`Reset()` restores the values the form was *constructed* with, which after a save round-trip are the
wrong ones. `Reset(values)` writes new ones and makes them the baseline, so `IsFormDirty`, an undo
button and an unsaved-changes prompt all mean what they say again:

```csharp
state.Reset(new Dictionary<string, object?>
{
    ["Customer"] = stored.Customer,
    ["Lines[0].Description"] = stored.Lines[0].Description,
});
```

`Snapshot()` is the other half: it maps every path the schema binds to onto its value — one entry per
repeater row included — which is exactly what `Reset(values)` takes back, so the two together are a
draft save and restore:

```csharp
var draft = state.Snapshot();     // stash it in session storage
// … the user comes back …
state.Reset(draft);
```

`ValidateFieldAsync(path)` checks one answer without judging the whole form, and `GetFieldState(path)`
answers everything about one field at once:

```csharp
await state.ValidateFieldAsync("Customer");

var field = state.GetFieldState("Customer");
// field.IsTouched, field.IsDirty, field.IsInvalid, field.Error, field.Messages
```

### Localisation

Register an `IBlazorFormMessageProvider` to replace the built-in English text — typically one wrapping `IStringLocalizer`:

```csharp
builder.Services.AddBlazorFormMessages<MyMessageProvider>();
```

It covers everything the library renders, not only the validation messages: the submit and reset
buttons, a wizard's Back/Next, the select placeholder and its loading state, a repeater's add/remove
labels and empty state, the error summary heading, the character counter. The keys are listed on
`BlazorFormMessageKeys`; anything a provider does not recognise should fall through to the key itself.

Messages passed explicitly to a rule — and labels passed explicitly to `BlazorFormView` — always win
over the provider.

### DataAnnotations and `IValidatableObject`

A schema generated from a model already carries its `[Required]`, `[StringLength]`, `[Range]` and
friends as field rules. What no attribute can express is a rule that reads more than one property —
and in .NET that is `IValidatableObject`:

```csharp
var state = new BlazorFormState(definition, new BlazorFormModelDataAccessor(model))
    .UseDataAnnotations();
```

Only the cross-property layer is added: running `Validator` over the properties again would put two
differently-worded copies of "required" under the same box. Pass
`UseDataAnnotations(includePropertyAttributes: true)` when the schema came from somewhere that never
saw those attributes — a JSON Schema document rendered over a typed model.

A result naming one member lands on that field, one naming several puts the same complaint under each
of them, and one naming none becomes a form-level message. `IValidatableObject.Validate` is called
directly rather than through `Validator.TryValidateObject`, which skips it entirely once any property
attribute has failed — the user would otherwise fix the last required field, submit again, and be told
about something new.

### FluentValidation

Wire up a FluentValidation validator on the form state. Failure property paths
(e.g. `Address.City`, `Items[0].Product`) map directly onto field paths, and failures landing on a
field the schema is currently hiding are dropped — a validator sees the whole model and knows nothing
about the form's conditions, so without that a rule on an abandoned branch would refuse the submit and
point at a control that is not on the page.

```csharp
using BlazorForm;

var state = new BlazorFormState(definition, new BlazorFormModelDataAccessor(model), serviceProvider)
    .UseFluentValidation(new ContactValidator());
```

```razor
<BlazorFormView State="state" OnValidSubmit="HandleValidSubmit" />
```

If your validators are registered in DI, call `state.UseFluentValidation()` (no argument) and the
matching `IValidator<TModel>` is resolved from the service provider.

`UseFluentValidation()` and `UseDataAnnotations()` compose, so a model that carries both a validator
and its own `IValidatableObject` can use both — a complaint they report identically on the same field
is still read once. `BlazorFormExternalValidator.CombineWith(...)` does the same for a hand-written
external validator.

## Conditional fields and wizards

Show, disable or conditionally require fields based on other values:

```csharp
.Field(x => x.CompanyName, f => f
    .VisibleWhen(nameof(Model.IsBusiness), BlazorFormConditionOperator.IsTrue)
    .RequiredWhen(nameof(Model.IsBusiness), BlazorFormConditionOperator.IsTrue)
    .ClearOnHide())
```

`ClearOnHide` empties the value as soon as the field disappears, so a branch the user abandoned
never contributes data to the submitted model.

Inside a repeater, a condition means **that row**. Paths are resolved against the object that owns the
field before falling back to the root, exactly as a computed value's dependencies are — so one row can
ask for an email while the next asks for a phone number:

```csharp
.Array(x => x.Contacts, row => row
    .Field(c => c.Kind, f => f.AsRadio().Options(("email", "Email"), ("phone", "Phone")))
    .Field(c => c.Email, f => f
        .VisibleWhen("Kind", BlazorFormConditionOperator.Equals, "email")   // this row's Kind
        .RequiredWhen("Kind", BlazorFormConditionOperator.Equals, "email")
        .ClearOnHide()))
```

Split a form into steps:

```csharp
BlazorFormBuilder.For<Order>()
    .Step("details", s => s.Title("Details").Fields("Name", "Email"))
    .Step("shipping", s => s.Title("Shipping").Fields("Address", "City", "Zip"))
    .Build();
```

`BlazorFormView` renders a stepper, Back/Next navigation, and validates each step before advancing.
Hidden steps are skipped and the visible ones stay contiguously numbered.

Every step the user has already walked past is clickable — forwards as well as back, since each was
validated on the way through — so coming back to fix one answer does not mean pressing Next through
the rest of the form. A step never reached stays inert, because jumping to it would skip the validation
that gates it. `AllowStepNavigation="false"` turns the links off entirely, and
`state.IsStepReachable(i)` / `state.FurthestStepIndex` expose the same information to a custom stepper.

A hidden step is hidden in every sense: its fields are not rendered, not validated on submit, and never
the reason a form refuses to submit. If a condition hides the step the user is standing on, the wizard
falls back to the nearest visible one rather than stranding them. Changing step moves focus to the new
step's content, so the change is not invisible to a keyboard or screen-reader user.

## Async and cascading options

Options can be loaded on demand and refreshed when another field changes:

```csharp
.Field(x => x.City, f => f
    .Required()
    .OptionsFrom(
        load:      async ctx => await cities.ForCountryAsync(ctx.Value("Country") as string, ctx.CancellationToken),
        value:     c => c.Code,
        label:     c => c.Name,
        dependsOn: nameof(Model.Country)))
```

When `Country` changes the cached options are dropped and the now-invalid selection is cleared.
Dependencies resolve against the object that owns the field before falling back to the root — exactly
as conditions and computed dependencies do — so `dependsOn: "Country"` inside a repeater means *that
row's* country, and naming a container (`dependsOn: "Address"`) covers everything inside it.

A lookup that goes over the network fails for ordinary reasons, so a provider that throws is recorded
rather than thrown: the control renders "options could not be loaded" instead of an empty dropdown, the
failure is available as `state.OptionsError(path)` and announced through `OptionsLoadFailed`, and
`InvalidateOptions(path)` retries.

```csharp
state.OptionsLoadFailed += (path, ex) => logger.LogWarning(ex, "Options for {Path} failed", path);
```

## Computed values

A field can derive its value from the rest of the form instead of being typed in — a line total, a
full name, a price after discount:

```csharp
BlazorFormBuilder.For<Invoice>()
    .Field(x => x.Customer, f => f.Required())
    .Array(x => x.Lines, line => line
        .Field(l => l.Quantity)
        .Field(l => l.UnitPrice)
        // Evaluated against the *line*, so it works on every row without knowing its index.
        .Computed(l => l.LineTotal, l => l.Quantity * l.UnitPrice,
                  dependsOn: ["Quantity", "UnitPrice"]))
    .Computed(x => x.Total, m => m.Lines.Sum(l => l.Quantity * l.UnitPrice),
              dependsOn: [nameof(Invoice.Lines)])
    .Build();
```

Computed fields are read-only by default, seeded when the form is created, and refreshed whenever a
declared dependency changes — including changes inside an array. One computed field may depend on
another; a set of formulas that reference each other in a cycle settles rather than recursing.

Dependencies are named relative to the object that owns the field, so `"Quantity"` on a repeater row
means that row's quantity. The untyped builder takes a context instead:
`f.Computed(ctx => ctx.Sibling("width"), "width")`.

## Combobox and tags

A `<select>` of two hundred countries is navigated by scrolling. A combobox is typed into:

```csharp
.Field(x => x.Country, f => f
    .Options(("fr", "France"), ("gb", "United Kingdom") /* … */)
    .AsCombobox()                      // closed: an answer on no list is reported
    .Clearable())

.Field(x => x.City, f => f
    .AsCombobox(allowCustom: true)     // the list proposes; anything is accepted
    .OptionsFrom(load: …, dependsOn: nameof(Model.Country)))
```

It is built on the browser's own `<input list>` + `<datalist>`, so filtering, keyboard navigation and
screen-reader support are the platform's. The library supplies the piece a bare datalist cannot: the
label the user reads is mapped onto the value the model stores, so an option can be `("fr", "France")`
rather than being forced to show its own key. Static options, `OptionsFromEnum` and async/cascading
`OptionsFrom` all work exactly as they do on a `<select>`.

A combobox always commits when the choice is made, whatever `UpdateOn` says: the box holds a label
and the model holds the value that label stands for, and half a label stands for nothing.

`AsCombobox()` adds a rule reporting an answer that is on no list — it is *reported*, never silently
discarded. The rule can only check options the schema itself declares; choices that arrive from an
`OptionsFrom` provider live in the form's runtime state rather than the schema, so validate those on
the server where the same lookup lives.

A list of short strings is a set of chips rather than a repeater:

```csharp
.Field(x => x.Skills, f => f.AsTags(max: 8))
```

Enter or a comma adds one (`Attr("tagSeparators", ";")` changes the set), backspace on an empty box
takes the last one back, and duplicates are refused case-insensitively. The entry box deliberately
belongs to no form, so pressing Enter adds a tag instead of posting the page.

## Change handlers

`Computed` derives a value the field owns. `OptionsFrom(dependsOn: …)` reloads a list. `ClearOnHide`
empties an abandoned branch. What none of them expresses is "when A changes, write B" where B is still
the user's to edit — so that is what `OnChange` is for:

```csharp
.Field(x => x.Plan, f => f
    .Options(("solo", "Solo"), ("team", "Team"))
    .OnChange(ctx => ctx.SetSibling(nameof(Model.Seats), ctx.Value is "team" ? 5 : 1)))

.Field(x => x.Country, f => f.OnChange(ctx => ctx.ClearSibling("City")))
```

Paths resolve relative to the object that owns the field, exactly as conditions and computed
dependencies do, so a handler on a repeater's item template means *that row*. What a handler writes
does not mark the field touched — the user has not been there, and a field the form filled in on their
behalf should not open covered in errors. Handlers do not run while the form is being constructed, and
a pair that answer each other settles rather than recursing.

## Layout

Set a column count on the form and let individual fields span more than one. The grid collapses to a
single column on narrow screens.

```csharp
BlazorFormBuilder.For<Profile>()
    .Columns(2)
    .Field(x => x.FirstName)
    .Field(x => x.LastName)
    .Field(x => x.Bio, f => f.AsTextArea(4).ColumnSpan(2))
    .Build();
```

### Groups

A run of consecutive fields sharing a group name renders as one `<fieldset>` with a `<legend>`, so a
screen reader announces "Who you are" as focus enters the first control in it. `[Display(GroupName =
…)]` on the model does the same thing.

```csharp
BlazorFormBuilder.For<SupportTicket>()
    .Field(x => x.Name,  f => f.Group("Who you are"))
    .Field(x => x.Email, f => f.Group("Who you are"))
    .Field(x => x.Team,  f => f.Group("Where it belongs"))
    .Build();
```

Grouping is by runs rather than by collecting every field with the same name, so the declared order is
never rearranged behind your back.

### Owning the layout

`BlazorFormView` renders the whole schema. When the page wants the layout for itself, place fields one
at a time instead — they keep the schema's label, rules, conditions and ARIA wiring:

```razor
<div class="two-up">
    <BlazorFormField State="_state" Name="ReporterName" />
    <BlazorFormField State="_state" Name="ReporterEmail" />
</div>

<h3>Routing</h3>
<BlazorFormField State="_state" Name="Team" />
<BlazorFormField State="_state" Name="Lines[0].Product" />

<button @onclick="() => _state.SubmitAsync(Save)">Raise ticket</button>
```

An unknown path renders nothing, which is what a schema chosen at run time needs; set
`ThrowIfMissing="true"` to catch a typo in a fixed layout.

## Field presentation

The details that separate a generated form from a designed one:

```csharp
BlazorFormBuilder.For<Listing>()
    // A section heading that belongs to the schema, not to the page around it.
    .Static("basics", "The basics", "What someone sees first in the search results.")

    .Field(x => x.Title, f => f
        .MaxLength(60)
        .CharacterCount()                       // a live "12 / 60"
        .UpdateOnInput())                       // write on every keystroke, not on blur

    .Field(x => x.Summary, f => f
        .AsTextArea(3)
        .UpdateOnInput(debounceMilliseconds: 150))  // one write per pause, not per character

    .Field(x => x.Price, f => f.Prefix("€").Suffix("/ month"))
    .Field(x => x.City, f => f
        .Suggest("Amsterdam", "Berlin", "Lisbon")   // proposes; does not restrict
        .Clearable())
    .Field(x => x.Passcode, f => f.AsPassword().Revealable())
    .Field(x => x.Published, f => f.AsSwitch())
    .Field(x => x.Query, f => f
        .HideLabel()                            // stays the accessible name
        .InputAttr("data-testid", "search"))    // any HTML attribute you like
    .Build();
```

`UpdateOnInput` matters more than it looks: the browser's `change` event fires on *blur* for a text
box, so without it the model does not see a keystroke until the user leaves the field. The `input`
handler is only wired when a field actually needs it, so nothing pays for a round-trip per keystroke
it has no use for.

## Checking a schema

`Validate()` reports the mistakes that are easy to make and hard to see. It is a development aid, never
run automatically — a schema being edited at runtime has every right to be incomplete:

```csharp
foreach (var problem in definition.Validate())
    logger.LogWarning("{Problem}", problem);   // [Error] Lines: An array field has no ItemTemplate…
```

## Service registration

Register BlazorForm's rendering services (the field renderer registry) in your DI container, and
optionally register custom renderers:

```csharp
builder.Services.AddBlazorForm(registry =>
{
    registry.RegisterCustom<StarRatingInput>("rating");
});
```

Custom input components inherit from `BlazorFormInputBase`. A field of type
`BlazorFormFieldType.Custom` whose renderer key is not registered throws with a message naming the
key, rather than silently falling back to a text box.

## The `BlazorFormView` component

| Parameter | Description |
| --- | --- |
| `Definition` | The schema to render. Required unless `State` is supplied. |
| `Model` | Optional typed model to bind to. When omitted, a dictionary store is used. |
| `Data` | Optional `IDictionary<string, object?>` backing store (used when `Model` is null). |
| `State` | Provide a pre-configured `BlazorFormState` (e.g. with FluentValidation wired up). |
| `OnValidSubmit` | Raised with the state after a successful (valid) submit. |
| `OnInvalidSubmit` | Raised after a submit that failed validation. |
| `OnFieldChanged` | Raised with the path of a field whose value changed. |
| `ReadOnly` | Renders every field read-only (review mode) and hides the buttons. |
| `Disabled` | Disables every field and button — a save in flight, a locked record. Unlike `ReadOnly` the fields cannot be tabbed through or read out, so reach for it only when the form is genuinely inoperable. |
| `ShowSubmitButton` / `SubmitText` | The built-in submit button (default `true` / `"Submit"`). |
| `ShowResetButton` / `ResetText` | A button restoring the values the form started with. |
| `ShowErrorSummary` | Lists every error above the form, each linking to its field. |
| `FocusFirstError` | Moves focus to the first invalid field after a failed submit (default `true`; suppressed while `ShowErrorSummary` is on, since the summary takes focus itself). |
| `AllowStepNavigation` | Lets the user click back to a completed wizard step (default `true`). |
| `BackText` / `NextText` | Wizard navigation labels. |
| `Header` / `Footer` / `Actions` | Render fragments for extra content and a custom button row. |
| `Class` | Extra CSS classes for the `<form>` element. |
| `ChildContent` | Extra content rendered inside the `<form>`. |

Any other attribute you set is splatted onto the `<form>` element.

When you let the view build its own state from `Definition`, capture the component to reach it:

```razor
<BlazorFormView @ref="_form" Definition="_definition" Model="_model" />

<button @onclick="() => _form.Form.Reset()" disabled="@(!_form.Form.IsFormDirty)">Undo</button>

@code { private BlazorFormView _form = default!; }
```

## `BlazorFormState` at a glance

| Member | Purpose |
| --- | --- |
| `GetValue` / `SetValue` / `SetValueQuietly` | Read and write by path; the quiet variant does not mark the field touched. |
| `SetValues` / `Batch` | Write many values as one change — one re-render, not one per field. |
| `IsFormDirty`, `DirtyFields`, `TouchedFields` | Change tracking. Dirtiness is a comparison against the values the form opened with, so a field typed into and put back is clean again. |
| `ResetField(path)` | Puts one field — and anything nested beneath it — back to the value it started with. |
| `IsValidating`, `IsSubmitting`, `IsSubmitted`, `SubmitCount`, `IsValid`, `HasValidated` | Submission state. `IsValid` reports what validation has *found*, so check `HasValidated` before treating it as a verdict — a form nobody has validated has no errors. |
| `GetFieldState(path)` | Touched, dirty, invalid, the first error and every message for one field, in a single read. |
| `SubmitAsync` | Marks everything touched, validates, and dispatches — ignoring re-entrant calls. |
| `ValidateAsync` / `ValidateStepAsync` / `ValidateFieldAsync` | Validation at three scopes; newer runs supersede older ones. `ValidateFieldAsync(path)` resolves the field from the schema for you. |
| `ValidateDependentsAsync(path)` | Revalidates every field that declared `RevalidateOn(path)`. The built-in controls call it for you; this is the hook for a custom one. |
| `SingleErrorPerField` | Show a field's first error rather than every rule it breaks. Every rule still runs. |
| `MessagesFor`, `AllMessages`, `OrderedMessages`, `SetServerError(s)` | Validation messages. |
| `Reset()` / `Reset(values)` / `AcceptChanges` | Restore the starting values, rebase onto new ones, or make the current ones the new baseline. |
| `Snapshot()` | Every bound path mapped onto its value — the shape `Reset(values)` takes back, so the two are a draft save and restore. |
| `AddArrayItem`, `InsertArrayItem`, `DuplicateArrayItem`, `RemoveArrayItem`, `MoveArrayItem`, `SwapArrayItems`, `ClearArrayItems` | Repeater operations; messages, touched and dirty state follow their items. |
| `FocusAsync(path)` | Moves focus to the control rendering a path; false when nothing is rendering it. |
| `Text(key, args)` | Resolves the library's own UI text through the registered message provider. |
| `NextStepAsync`, `PreviousStep`, `GoToStep`, `VisibleSteps`, `CurrentStepNumber`, `FurthestStepIndex`, `IsStepReachable` | Wizard navigation. |
| `ReadOnly` / `Disabled` | Makes the whole form read-only (still focusable and readable) or disabled (inoperable). |

## Key types

| Type | Purpose |
| --- | --- |
| `BlazorFormDefinition` | The schema: fields, wizard steps, form-level rules and metadata. `Clone()` for a shared schema you want to tailor. |
| `BlazorFormFieldDefinition` / `BlazorFormFieldType` | A single field and its logical type. |
| `BlazorFormBuilder` / `BlazorFormBuilder<TModel>` | Fluent schema builders. |
| `BlazorFormSchemaGenerator` | Reflection + DataAnnotations schema generation. |
| `BlazorFormJsonSchemaImporter` / `BlazorFormJsonSchemaExporter` | JSON Schema import/export. |
| `BlazorFormState` | Runtime state: data, validation, dirty/touched tracking, wizard position. |
| `BlazorFormView` / `BlazorFormField` | Render a whole schema, or one field of it inside your own layout. |
| `BlazorFormModelDataAccessor` / `BlazorFormDictionaryDataAccessor` | Data backing stores. |
| `IBlazorFormFieldRendererRegistry` | Maps field types to renderer components. |
| `IBlazorFormMessageProvider` | Supplies the text of built-in validation messages. |

## Project layout

```
src/BlazorForm
├── Building/      Fluent form & field builders
├── Components/    Razor components (BlazorFormView, field, input and summary views)
├── Data/          Data accessors (model & dictionary) and form paths
├── Generation/    Reflection + DataAnnotations schema generation
├── Json/          JSON Schema import/export
├── Rendering/     Field context, value conversion and renderer registry
├── State/         Runtime form state
├── Validation/    Validation rules, messages and the validator
└── Schema/        Field types and schema model
```

## Changelog

See [CHANGELOG.md](CHANGELOG.md).

## License

Licensed under the [MIT License](LICENSE).
