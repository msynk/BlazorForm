namespace BlazorForm.Tests;

/// <summary>
/// The README promises that UI intent survives a trip through JSON. A field option that exports but
/// does not import — or neither — quietly turns "store this form in a database" into "store most of it".
/// </summary>
public class JsonRoundTripTests
{
    private static BlazorFormDefinition RoundTrip(BlazorFormDefinition form)
        => BlazorFormJsonSchemaImporter.Import(BlazorFormJsonSchemaExporter.Export(form));

    [Fact]
    public void Presentation_options_survive_a_round_trip()
    {
        var original = BlazorFormBuilder.Create()
            .Text("title", f => f
                .Label("Title")
                .Prefix("#").Suffix("!")
                .MaxLength(60).CharacterCount()
                .HideLabel()
                .Suggest("one", "two")
                .UpdateOnInput(debounceMilliseconds: 120)
                .InputAttr("data-testid", "title")
                .Autofocus())
            .Build();

        var field = RoundTrip(original).FindField("title")!;

        Assert.Equal("#", field.Prefix);
        Assert.Equal("!", field.Suffix);
        Assert.True(field.ShowCharacterCount);
        Assert.False(field.ShowLabel);
        Assert.Equal(["one", "two"], field.Suggestions);
        Assert.Equal(BlazorFormUpdateTrigger.Input, field.UpdateOn);
        Assert.Equal(120, field.DebounceMilliseconds);
        Assert.Equal("title", field.InputAttributes["data-testid"]);
        Assert.True(field.Autofocus);
    }

    [Fact]
    public void A_ui_step_does_not_come_back_as_a_multipleOf_constraint()
    {
        // A price spinner stepping by 0.01 is not a promise that 0.005 is invalid, and exporting it as
        // multipleOf would invent a rule the author never wrote.
        var original = BlazorFormBuilder.Create()
            .Number("price", f => f.Step(0.01))
            .Build();

        var json = BlazorFormJsonSchemaExporter.Export(original);
        var field = RoundTrip(original).FindField("price")!;

        Assert.DoesNotContain("multipleOf", json, StringComparison.Ordinal);
        Assert.Equal(0.01, field.NumericStep);
        Assert.DoesNotContain(field.Validators, v => v.Key == "multipleOf");
    }

    [Fact]
    public void A_real_multipleOf_rule_still_round_trips_as_one()
    {
        var original = BlazorFormBuilder.Create()
            .Number("qty", f => f.MultipleOf(5))
            .Build();

        var field = RoundTrip(original).FindField("qty")!;

        Assert.Contains(field.Validators, v => v.Key == "multipleOf");
        Assert.Equal(5, field.NumericStep);
    }

    [Fact]
    public void Renderer_hints_and_custom_renderers_survive()
    {
        var original = BlazorFormBuilder.Create()
            .Checkbox("notify", f => f.AsSwitch())
            .Field("stars", BlazorFormFieldType.Custom, f => f.CustomRenderer("rating").Attr("max", 5))
            .Build();

        var form = RoundTrip(original);

        var notify = form.FindField("notify")!;
        Assert.Equal(BlazorFormFieldType.Checkbox, notify.Type);
        Assert.Equal(true, notify.Attributes["switch"]);

        var stars = form.FindField("stars")!;
        Assert.Equal(BlazorFormFieldType.Custom, stars.Type);
        Assert.Equal("rating", stars.CustomRenderer);
        Assert.Equal(5d, Convert.ToDouble(stars.Attributes["max"], System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Static_content_survives_as_a_static_widget()
    {
        var original = BlazorFormBuilder.Create()
            .Static("intro", "Your details", "We only use these to contact you.")
            .Text("name")
            .Build();

        var field = RoundTrip(original).FindField("intro")!;

        Assert.Equal(BlazorFormFieldType.Static, field.Type);
        Assert.Equal("Your details", field.Label);
        Assert.Equal("We only use these to contact you.", field.HelpText);
    }

    [Fact]
    public void A_repeaters_item_noun_and_size_bounds_survive()
    {
        var original = BlazorFormBuilder.Create()
            .ArrayOf("tags", BlazorFormFieldType.Text, f => f
                .Items(min: 1, max: 5)
                .UniqueItems()
                .Attr("itemNoun", "tag")
                .Attr("duplicable", true))
            .Build();

        var field = RoundTrip(original).FindField("tags")!;

        Assert.Equal(1, field.MinItems);
        Assert.Equal(5, field.MaxItems);
        Assert.Contains(field.Validators, v => v.Key == "uniqueItems");
        Assert.Equal("tag", field.Attributes["itemNoun"]);
        Assert.Equal(true, field.Attributes["duplicable"]);
    }

    [Fact]
    public void An_optional_value_written_as_a_null_union_imports_as_the_value_it_wraps()
    {
        // How most generators spell "optional string". Treating it as an untyped text box would lose
        // the format, the bounds and everything else the real branch carries.
        const string json = """
        {
          "type": "object",
          "properties": {
            "website": {
              "title": "Website",
              "anyOf": [ { "type": "string", "format": "uri", "maxLength": 200 }, { "type": "null" } ]
            }
          }
        }
        """;

        var field = BlazorFormJsonSchemaImporter.Import(json).FindField("website")!;

        Assert.Equal(BlazorFormFieldType.Url, field.Type);
        Assert.Equal(200, field.MaxLength);
        // The outer title wins: it describes the field, not the branch.
        Assert.Equal("Website", field.Label);
    }

    [Fact]
    public void A_oneOf_of_const_values_imports_as_a_labelled_select()
    {
        // The standard way to give an enum member a display name, since `enum` has nowhere to put one.
        const string json = """
        {
          "type": "object",
          "properties": {
            "size": {
              "title": "Size",
              "oneOf": [
                { "const": "s", "title": "Small" },
                { "const": "l", "title": "Large" }
              ]
            }
          }
        }
        """;

        var field = BlazorFormJsonSchemaImporter.Import(json).FindField("size")!;

        Assert.Equal(BlazorFormFieldType.Select, field.Type);
        Assert.Equal(["s", "l"], field.Options.Select(o => o.Value));
        Assert.Equal(["Small", "Large"], field.Options.Select(o => o.Label));
    }

    [Fact]
    public void A_union_of_object_shapes_is_left_alone_rather_than_guessed_at()
    {
        // Choosing a branch would bind the user's answers to a shape the document never committed to.
        // Importing it as a plain field is honest; silently picking the first branch would not be.
        const string json = """
        {
          "type": "object",
          "properties": {
            "payment": {
              "title": "Payment",
              "oneOf": [
                { "type": "object", "properties": { "cardNumber": { "type": "string" } } },
                { "type": "object", "properties": { "iban": { "type": "string" } } }
              ]
            }
          }
        }
        """;

        var field = BlazorFormJsonSchemaImporter.Import(json).FindField("payment")!;

        Assert.Empty(field.Children);
        Assert.Equal("Payment", field.Label);
    }

    [Fact]
    public void A_hint_with_no_json_form_is_dropped_rather_than_approximated()
    {
        // Same principle the conditions follow: code has no JSON representation, so it is omitted
        // instead of being turned into something that merely looks like it round-tripped.
        var original = BlazorFormBuilder.Create()
            .Text("x", f => f.Attr("callback", new Action(() => { })).Attr("rows", 3))
            .Build();

        var field = RoundTrip(original).FindField("x")!;

        Assert.False(field.Attributes.ContainsKey("callback"));
        Assert.Equal(3d, Convert.ToDouble(field.Attributes["rows"], System.Globalization.CultureInfo.InvariantCulture));
    }
}
