using System.Globalization;

namespace BlazorForm.Tests;

public class ValueConverterTests
{
    [Fact]
    public void Formats_dates_and_times_for_html_inputs()
    {
        Assert.Equal("2024-03-05", BlazorFormValueConverter.ToInputString(new DateTime(2024, 3, 5, 14, 30, 0), BlazorFormFieldType.Date));
        Assert.Equal("14:30", BlazorFormValueConverter.ToInputString(new DateTime(2024, 3, 5, 14, 30, 0), BlazorFormFieldType.Time));
        Assert.Equal("2024-03-05T14:30", BlazorFormValueConverter.ToInputString(new DateTime(2024, 3, 5, 14, 30, 0), BlazorFormFieldType.DateTime));
        Assert.Equal("2024-03-05", BlazorFormValueConverter.ToInputString(new DateOnly(2024, 3, 5), BlazorFormFieldType.Date));
        Assert.Equal("09:15", BlazorFormValueConverter.ToInputString(new TimeOnly(9, 15), BlazorFormFieldType.Time));
    }

    [Fact]
    public void Formats_timespan_as_html_time_not_dotnet_default()
    {
        // TimeSpan.ToString() would produce "02:30:00", which <input type="time"> rejects.
        Assert.Equal("02:30", BlazorFormValueConverter.ToInputString(TimeSpan.FromMinutes(150), BlazorFormFieldType.Time));
    }

    [Fact]
    public void Parses_timespan_from_html_time_value()
    {
        var parsed = BlazorFormValueConverter.FromInputString("02:30", typeof(TimeSpan), BlazorFormFieldType.Time);
        Assert.Equal(TimeSpan.FromMinutes(150), parsed);
    }

    [Fact]
    public void Numbers_round_trip_invariantly_under_a_comma_decimal_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            Assert.Equal("1.5", BlazorFormValueConverter.ToInputString(1.5d, BlazorFormFieldType.Number));
            Assert.Equal("1.5", BlazorFormValueConverter.ToInputString(1.5m, BlazorFormFieldType.Number));
            Assert.Equal(1.5m, BlazorFormValueConverter.FromInputString("1.5", typeof(decimal), BlazorFormFieldType.Number));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Empty_input_clears_a_nullable_and_defaults_a_value_type()
    {
        Assert.Null(BlazorFormValueConverter.FromInputString("", typeof(int?), BlazorFormFieldType.Integer));
        Assert.Equal(0, BlazorFormValueConverter.FromInputString("", typeof(int), BlazorFormFieldType.Integer));
        Assert.Null(BlazorFormValueConverter.FromInputString("", typeof(string), BlazorFormFieldType.Text));
    }

    [Fact]
    public void Unparseable_input_is_returned_unchanged_so_the_user_can_fix_it()
    {
        Assert.Equal("abc", BlazorFormValueConverter.FromInputString("abc", typeof(int), BlazorFormFieldType.Integer));
    }

    [Fact]
    public void Coerces_a_string_list_into_a_typed_enum_list()
    {
        var ok = BlazorFormValueConverter.TryCoerce(
            new List<string> { "Business", "Personal" }, typeof(List<AccountType>), out var result);

        Assert.True(ok);
        var list = Assert.IsType<List<AccountType>>(result);
        Assert.Equal([AccountType.Business, AccountType.Personal], list);
    }

    [Fact]
    public void Coerces_a_string_list_into_an_array()
    {
        var ok = BlazorFormValueConverter.TryCoerce(new List<string> { "a", "b" }, typeof(string[]), out var result);
        Assert.True(ok);
        Assert.Equal(["a", "b"], Assert.IsType<string[]>(result));
    }

    [Fact]
    public void Reports_failure_rather_than_throwing_on_an_impossible_conversion()
    {
        Assert.False(BlazorFormValueConverter.TryCoerce("not-a-number", typeof(int), out _));
    }

    [Fact]
    public void Invariant_string_is_stable_across_cultures()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            Assert.Equal("1.25", BlazorFormValueConverter.ToInvariantString(1.25d));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}

public class TypedAccessorTests
{
    [Fact]
    public void Creates_array_elements_of_the_declared_type()
    {
        var model = new RegistrationModel();
        var data = new BlazorFormModelDataAccessor(model);

        // Items is empty: writing through an index has to materialise a LineItem, not a bare object.
        data.SetValue("Items[0].Product", "Widget");

        Assert.Single(model.Items);
        Assert.Equal("Widget", model.Items[0].Product);
        Assert.False(data.LastWriteFailed);
    }

    [Fact]
    public void An_unconvertible_write_is_reported_not_thrown()
    {
        var model = new TypedModel { Count = 7 };
        var data = new BlazorFormModelDataAccessor(model);

        data.SetValue("Count", "not-a-number");

        Assert.True(data.LastWriteFailed);
        Assert.Equal(7, model.Count); // last valid value preserved
    }

    [Fact]
    public void Writes_multi_select_strings_into_a_typed_enum_collection()
    {
        var model = new TypedModel();
        var data = new BlazorFormModelDataAccessor(model);

        data.SetValue("Accounts", new List<string> { "Business" });

        Assert.Equal([AccountType.Business], model.Accounts);
    }

    [Fact]
    public void Writes_scalars_of_every_supported_shape()
    {
        var model = new TypedModel();
        var data = new BlazorFormModelDataAccessor(model);

        data.SetValue("Duration", "01:45");
        data.SetValue("Day", "2024-03-05");
        data.SetValue("Moment", "09:15");
        data.SetValue("Id", "8a1e2f3d-0000-4000-8000-000000000001");
        data.SetValue("Website", "https://example.com");
        data.SetValue("OptionalCount", "");

        Assert.Equal(new TimeSpan(1, 45, 0), model.Duration);
        Assert.Equal(new DateOnly(2024, 3, 5), model.Day);
        Assert.Equal(new TimeOnly(9, 15), model.Moment);
        Assert.Equal(Guid.Parse("8a1e2f3d-0000-4000-8000-000000000001"), model.Id);
        Assert.Equal(new Uri("https://example.com"), model.Website);
        Assert.Null(model.OptionalCount);
    }

    [Fact]
    public void Resolves_element_types_for_lists_and_arrays()
    {
        var data = new BlazorFormModelDataAccessor(new TypedModel());
        Assert.Equal(typeof(AccountType), data.GetElementType("Accounts"));
        Assert.Equal(typeof(string), data.GetElementType("Tags"));
    }
}

public class PathTests
{
    [Fact]
    public void Rejects_a_non_numeric_index_with_a_useful_message()
    {
        var ex = Assert.Throws<FormatException>(() => BlazorFormPath.Parse("items[x]"));
        Assert.Contains("items[x]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_an_unterminated_index()
        => Assert.Throws<FormatException>(() => BlazorFormPath.Parse("items[0"));

    [Fact]
    public void Recognises_paths_inside_a_subtree()
    {
        Assert.True(BlazorFormPath.IsAtOrUnder("Items", "Items"));
        Assert.True(BlazorFormPath.IsAtOrUnder("Items[0].Product", "Items"));
        Assert.True(BlazorFormPath.IsAtOrUnder("Address.City", "Address"));
        Assert.False(BlazorFormPath.IsAtOrUnder("ItemsTotal", "Items"));
    }

    [Fact]
    public void Reindexes_array_element_paths()
    {
        Assert.Equal("Items[1].Product", BlazorFormPath.Reindex("Items[2].Product", "Items", 1));
        Assert.Equal(2, BlazorFormPath.IndexIn("Items[2].Product", "Items"));
        Assert.Null(BlazorFormPath.IndexIn("Other[2]", "Items"));
    }
}
