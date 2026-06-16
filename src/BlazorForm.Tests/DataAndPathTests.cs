using BlazorForm.Core.Data;

namespace BlazorForm.Tests;

public class FormPathTests
{
    [Fact]
    public void Parses_dotted_and_indexed_paths()
    {
        var segments = FormPath.Parse("items[2].product");
        Assert.Equal(3, segments.Count);
        Assert.Equal("items", segments[0].Name);
        Assert.True(segments[1].IsIndex);
        Assert.Equal(2, segments[1].Index);
        Assert.Equal("product", segments[2].Name);
    }

    [Fact]
    public void Combine_builds_paths()
    {
        Assert.Equal("a.b", FormPath.Combine("a", "b"));
        Assert.Equal("b", FormPath.Combine("", "b"));
        Assert.Equal("a[3]", FormPath.Combine("a", 3));
    }
}

public class DictionaryDataAccessorTests
{
    [Fact]
    public void Sets_and_gets_nested_values()
    {
        var data = new DictionaryDataAccessor();
        data.SetValue("address.city", "Paris");
        Assert.Equal("Paris", data.GetValue("address.city"));
    }

    [Fact]
    public void Sets_and_gets_array_values()
    {
        var data = new DictionaryDataAccessor();
        data.SetValue("items[0].product", "Widget");
        data.SetValue("items[1].product", "Gadget");
        Assert.Equal("Widget", data.GetValue("items[0].product"));
        Assert.Equal("Gadget", data.GetValue("items[1].product"));
    }

    [Fact]
    public void Missing_paths_return_null()
    {
        var data = new DictionaryDataAccessor();
        Assert.Null(data.GetValue("nope.missing"));
        Assert.Null(data.GetValue("arr[5]"));
    }
}

public class ModelDataAccessorTests
{
    [Fact]
    public void Reads_and_writes_nested_properties()
    {
        var model = new RegistrationModel();
        var data = new ModelDataAccessor(model);

        data.SetValue("FirstName", "Ada");
        data.SetValue("Address.City", "London");

        Assert.Equal("Ada", model.FirstName);
        Assert.Equal("London", model.Address.City);
        Assert.Equal("Ada", data.GetValue("FirstName"));
        Assert.Equal("London", data.GetValue("Address.City"));
    }

    [Fact]
    public void Converts_string_to_target_type()
    {
        var model = new RegistrationModel();
        var data = new ModelDataAccessor(model);
        data.SetValue("Age", "42");
        Assert.Equal(42, model.Age);
    }

    [Fact]
    public void Resolves_array_element_type()
    {
        var data = new ModelDataAccessor(new RegistrationModel());
        Assert.Equal(typeof(LineItem), data.GetElementType("Items"));
    }
}
