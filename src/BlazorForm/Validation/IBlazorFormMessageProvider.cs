namespace BlazorForm;

/// <summary>
/// Supplies the text of built-in validation messages. Register an implementation in DI to localise
/// BlazorForm without replacing its rules; the provider is resolved from
/// <see cref="BlazorFormValidationContext.Services"/> at validation time, so the same schema can render
/// in different languages per request.
/// </summary>
public interface IBlazorFormMessageProvider
{
    /// <summary>
    /// Returns the message for <paramref name="key"/> (see <see cref="BlazorFormMessageKeys"/>),
    /// formatted with <paramref name="args"/>.
    /// </summary>
    string Get(string key, params object?[] args);
}

/// <summary>The keys used by the built-in validation rules when asking an <see cref="IBlazorFormMessageProvider"/> for text.</summary>
public static class BlazorFormMessageKeys
{
    public const string Required = "required";
    public const string MinLength = "minLength";
    public const string MaxLength = "maxLength";
    public const string RangeBetween = "range.between";
    public const string RangeMin = "range.min";
    public const string RangeMax = "range.max";
    public const string Pattern = "pattern";
    public const string Email = "email";
    public const string Url = "url";
    public const string Compare = "compare";
    public const string MinItems = "items.min";
    public const string MaxItems = "items.max";
    public const string UniqueItems = "items.unique";
    public const string MultipleOf = "multipleOf";
    public const string FileSize = "file.size";
    public const string FileType = "file.type";
    public const string PatternTimeout = "pattern.timeout";
    public const string Conversion = "conversion";

    // --- UI chrome ---
    // The library renders text of its own — buttons, placeholders, the empty state of a repeater. It
    // goes through the same provider as the validation messages, so translating a form means
    // implementing one interface rather than one interface plus a dozen component parameters.

    /// <summary>Placeholder shown in a select with nothing chosen.</summary>
    public const string SelectPlaceholder = "ui.select.placeholder";

    /// <summary>Placeholder shown while an options provider is loading.</summary>
    public const string SelectLoading = "ui.select.loading";

    /// <summary>Placeholder shown when an options provider failed, so an empty list is explained rather than mysterious.</summary>
    public const string SelectError = "ui.select.error";

    /// <summary>Default noun for one repeater row ("item"), used to build the buttons' labels.</summary>
    public const string ArrayItem = "ui.array.item";

    /// <summary>Label of a repeater's add button. Arg 0 is the item noun.</summary>
    public const string ArrayAdd = "ui.array.add";

    /// <summary>Accessible name of a row's remove button. Args: item noun, 1-based row number.</summary>
    public const string ArrayRemove = "ui.array.remove";

    /// <summary>Visible text of a row's remove button, with no room for the row number.</summary>
    public const string ArrayRemoveShort = "ui.array.remove.short";

    /// <summary>Live-region text announcing a repeater's size. Args: count, item noun.</summary>
    public const string ArrayCount = "ui.array.count";

    /// <summary>Label of a row's duplicate button. Args: item noun, 1-based row number.</summary>
    public const string ArrayDuplicate = "ui.array.duplicate";

    /// <summary>Label of a row's "move up" button. Args: item noun, 1-based row number.</summary>
    public const string ArrayMoveUp = "ui.array.moveUp";

    /// <summary>Label of a row's "move down" button. Args: item noun, 1-based row number.</summary>
    public const string ArrayMoveDown = "ui.array.moveDown";

    /// <summary>Text shown in place of an empty repeater. Arg 0 is the item noun.</summary>
    public const string ArrayEmpty = "ui.array.empty";

    /// <summary>
    /// Accessible name of one repeater row, so a screen reader says which row a control belongs to.
    /// Args: item noun, 1-based row number.
    /// </summary>
    public const string ArrayItemGroup = "ui.array.itemGroup";

    /// <summary>Label of the button that clears a file field's selection.</summary>
    public const string FileClear = "ui.file.clear";

    /// <summary>Heading of the error summary when it lists exactly one problem.</summary>
    public const string SummaryTitleOne = "ui.summary.title.one";

    /// <summary>Heading of the error summary. Arg 0 is the number of problems.</summary>
    public const string SummaryTitleMany = "ui.summary.title.many";

    /// <summary>Label of the submit button.</summary>
    public const string Submit = "ui.submit";

    /// <summary>Label of the reset button.</summary>
    public const string Reset = "ui.reset";

    /// <summary>Label of a wizard's "back" button.</summary>
    public const string Back = "ui.back";

    /// <summary>Label of a wizard's "next" button.</summary>
    public const string Next = "ui.next";

    /// <summary>Accessible name of a wizard's stepper landmark.</summary>
    public const string Progress = "ui.progress";

    /// <summary>Announced when the wizard's position changes. Args: current step, total visible steps.</summary>
    public const string StepOf = "ui.step.of";

    /// <summary>Accessible name of the button that reveals a password. </summary>
    public const string PasswordShow = "ui.password.show";

    /// <summary>Accessible name of the button that hides a revealed password.</summary>
    public const string PasswordHide = "ui.password.hide";

    /// <summary>Accessible name of the button that empties a field.</summary>
    public const string Clear = "ui.clear";

    /// <summary>Character counter beneath a length-limited input. Args: used, limit.</summary>
    public const string CharacterCount = "ui.characters";

    /// <summary>
    /// Announced to assistive technology as a length-limited field approaches its limit. Arg 0 is the
    /// number of characters left. The visible counter is decorative; this is what a screen-reader user
    /// actually hears.
    /// </summary>
    public const string CharactersRemaining = "ui.characters.remaining";

    /// <summary>Announced once a length-limited field is over its limit. Arg 0 is the excess.</summary>
    public const string CharactersOver = "ui.characters.over";
}

/// <summary>The English defaults used when no <see cref="IBlazorFormMessageProvider"/> is registered.</summary>
public sealed class BlazorFormDefaultMessageProvider : IBlazorFormMessageProvider
{
    /// <summary>A shared instance; the rules fall back to this when DI has nothing registered.</summary>
    public static readonly BlazorFormDefaultMessageProvider Instance = new();

    public string Get(string key, params object?[] args) => key switch
    {
        BlazorFormMessageKeys.Required => "This field is required.",
        BlazorFormMessageKeys.MinLength => $"Must be at least {Arg(args, 0)} characters.",
        BlazorFormMessageKeys.MaxLength => $"Must be at most {Arg(args, 0)} characters.",
        BlazorFormMessageKeys.RangeBetween => $"Must be between {Arg(args, 0)} and {Arg(args, 1)}.",
        BlazorFormMessageKeys.RangeMin => $"Must be at least {Arg(args, 0)}.",
        BlazorFormMessageKeys.RangeMax => $"Must be at most {Arg(args, 0)}.",
        BlazorFormMessageKeys.Pattern => "Invalid format.",
        BlazorFormMessageKeys.PatternTimeout => "This value could not be checked; please simplify it.",
        BlazorFormMessageKeys.Email => "Enter a valid email address.",
        BlazorFormMessageKeys.Url => "Enter a valid URL.",
        BlazorFormMessageKeys.Compare => $"Must match {Arg(args, 0)}.",
        BlazorFormMessageKeys.MinItems => $"Add at least {Arg(args, 0)} item(s).",
        BlazorFormMessageKeys.MaxItems => $"No more than {Arg(args, 0)} item(s) allowed.",
        BlazorFormMessageKeys.UniqueItems => "Items must be unique.",
        BlazorFormMessageKeys.MultipleOf => $"Must be a multiple of {Arg(args, 0)}.",
        BlazorFormMessageKeys.FileSize => $"Each file must be {Arg(args, 0)} or smaller.",
        BlazorFormMessageKeys.FileType => $"Only these file types are allowed: {Arg(args, 0)}.",
        BlazorFormMessageKeys.Conversion => $"'{Arg(args, 0)}' is not a valid value for this field.",

        BlazorFormMessageKeys.SelectPlaceholder => "-- Select --",
        BlazorFormMessageKeys.SelectLoading => "Loading…",
        BlazorFormMessageKeys.SelectError => "Options could not be loaded",
        BlazorFormMessageKeys.ArrayItem => "item",
        BlazorFormMessageKeys.ArrayAdd => $"Add {Arg(args, 0)}",
        BlazorFormMessageKeys.ArrayRemove => $"Remove {Arg(args, 0)} {Arg(args, 1)}",
        BlazorFormMessageKeys.ArrayRemoveShort => "Remove",
        BlazorFormMessageKeys.ArrayCount => $"{Arg(args, 0)} {Arg(args, 1)}{(Arg(args, 0) is 1 ? "" : "s")}",
        BlazorFormMessageKeys.ArrayDuplicate => $"Duplicate {Arg(args, 0)} {Arg(args, 1)}",
        BlazorFormMessageKeys.ArrayMoveUp => $"Move {Arg(args, 0)} {Arg(args, 1)} up",
        BlazorFormMessageKeys.ArrayMoveDown => $"Move {Arg(args, 0)} {Arg(args, 1)} down",
        BlazorFormMessageKeys.ArrayEmpty => $"No {Arg(args, 0)}s yet.",
        BlazorFormMessageKeys.ArrayItemGroup => $"{Arg(args, 0)} {Arg(args, 1)}",
        BlazorFormMessageKeys.FileClear => "Clear selection",
        BlazorFormMessageKeys.SummaryTitleOne => "There is a problem with this form:",
        BlazorFormMessageKeys.SummaryTitleMany => $"There are {Arg(args, 0)} problems with this form:",
        BlazorFormMessageKeys.Submit => "Submit",
        BlazorFormMessageKeys.Reset => "Reset",
        BlazorFormMessageKeys.Back => "Back",
        BlazorFormMessageKeys.Next => "Next",
        BlazorFormMessageKeys.Progress => "Progress",
        BlazorFormMessageKeys.StepOf => $"Step {Arg(args, 0)} of {Arg(args, 1)}",
        BlazorFormMessageKeys.PasswordShow => "Show password",
        BlazorFormMessageKeys.PasswordHide => "Hide password",
        BlazorFormMessageKeys.Clear => "Clear",
        BlazorFormMessageKeys.CharacterCount => $"{Arg(args, 0)} / {Arg(args, 1)}",
        BlazorFormMessageKeys.CharactersRemaining => $"{Arg(args, 0)} characters remaining",
        BlazorFormMessageKeys.CharactersOver => $"{Arg(args, 0)} characters over the limit",

        _ => key
    };

    private static object? Arg(object?[] args, int index) => index < args.Length ? args[index] : null;
}
