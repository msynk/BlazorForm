namespace BlazorForm.Tests;

public class OrderModel
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string FullName { get; set; } = "";
    public List<OrderLine> Lines { get; set; } = [];
    public decimal Total { get; set; }
    public decimal TotalWithTax { get; set; }
}

public class OrderLine
{
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class ComputedValueTests
{
    private static BlazorFormDefinition Schema() => BlazorFormBuilder.For<OrderModel>()
        .Field(x => x.FirstName)
        .Field(x => x.LastName)
        .Computed(x => x.FullName, m => $"{m.FirstName} {m.LastName}".Trim(),
            dependsOn: [nameof(OrderModel.FirstName), nameof(OrderModel.LastName)])
        .Array(x => x.Lines, line => line
            .Field(l => l.Quantity)
            .Field(l => l.UnitPrice)
            .Computed(l => l.LineTotal, l => l.Quantity * l.UnitPrice,
                dependsOn: [nameof(OrderLine.Quantity), nameof(OrderLine.UnitPrice)]))
        .Computed(x => x.Total, m => m.Lines.Sum(l => l.Quantity * l.UnitPrice),
            dependsOn: [nameof(OrderModel.Lines)])
        // Chained: depends on another computed field.
        .Computed(x => x.TotalWithTax, m => Math.Round(m.Total * 1.2m, 2),
            dependsOn: [nameof(OrderModel.Total)])
        .Build();

    [Fact]
    public void A_computed_field_refreshes_when_a_dependency_changes()
    {
        var model = new OrderModel();
        var state = new BlazorFormState(Schema(), new BlazorFormModelDataAccessor(model));

        state.SetValue("FirstName", "Ada");
        state.SetValue("LastName", "Lovelace");

        Assert.Equal("Ada Lovelace", model.FullName);
    }

    [Fact]
    public void An_unrelated_change_does_not_recompute()
    {
        var model = new OrderModel { FirstName = "Ada" };
        var form = BlazorFormBuilder.For<OrderModel>()
            .Field(x => x.FirstName)
            .Field(x => x.LastName)
            .Computed(x => x.FullName, m => m.FirstName, dependsOn: [nameof(OrderModel.FirstName)])
            .Build();

        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));
        Assert.Equal("Ada", model.FullName);

        model.FullName = "manually overridden";
        state.SetValue("LastName", "Lovelace");

        Assert.Equal("manually overridden", model.FullName);
    }

    [Fact]
    public void Computed_values_are_seeded_when_the_form_is_created()
    {
        var model = new OrderModel { FirstName = "Grace", LastName = "Hopper" };

        _ = new BlazorFormState(Schema(), new BlazorFormModelDataAccessor(model));

        Assert.Equal("Grace Hopper", model.FullName);
    }

    [Fact]
    public void A_formula_on_an_array_item_reads_that_item_not_the_root()
    {
        // The interesting case: the same expression has to work on row 0 and row 1 without knowing
        // which row it is on.
        var model = new OrderModel();
        var form = Schema();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));
        var lines = form.FindField("Lines")!;

        state.AddArrayItem(lines, "Lines");
        state.AddArrayItem(lines, "Lines");

        state.SetValue("Lines[0].Quantity", 3);
        state.SetValue("Lines[0].UnitPrice", 10m);
        state.SetValue("Lines[1].Quantity", 2);
        state.SetValue("Lines[1].UnitPrice", 5.5m);

        Assert.Equal(30m, model.Lines[0].LineTotal);
        Assert.Equal(11m, model.Lines[1].LineTotal);
    }

    [Fact]
    public void A_change_inside_an_array_item_refreshes_a_total_over_the_whole_array()
    {
        var model = new OrderModel();
        var form = Schema();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));
        var lines = form.FindField("Lines")!;

        state.AddArrayItem(lines, "Lines");
        state.SetValue("Lines[0].Quantity", 4);
        state.SetValue("Lines[0].UnitPrice", 25m);

        Assert.Equal(100m, model.Total);
        // ...and a computed field that depends on another computed field follows along.
        Assert.Equal(120m, model.TotalWithTax);
    }

    [Fact]
    public void Removing_an_item_refreshes_the_total()
    {
        var model = new OrderModel();
        var form = Schema();
        var state = new BlazorFormState(form, new BlazorFormModelDataAccessor(model));
        var lines = form.FindField("Lines")!;

        state.AddArrayItem(lines, "Lines");
        state.SetValue("Lines[0].UnitPrice", 40m);
        Assert.Equal(40m, model.Total);

        state.RemoveArrayItem("Lines", 0);

        Assert.Equal(0m, model.Total);
    }

    [Fact]
    public void A_computed_field_is_read_only_by_default()
        => Assert.True(Schema().FindField("Total")!.ReadOnly);

    [Fact]
    public void Circular_formulas_settle_instead_of_recursing_forever()
    {
        // Two fields that each derive from the other never converge; the engine has to stop rather
        // than blow the stack.
        var form = BlazorFormBuilder.Create()
            .Text("trigger")
            .Integer("a", f => f.Computed(ctx => Convert.ToInt32(ctx.Value("b") ?? 0) + 1))
            .Integer("b", f => f.Computed(ctx => Convert.ToInt32(ctx.Value("a") ?? 0) + 1))
            .Build();

        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());

        state.SetValue("trigger", "go");

        // No exception, and both fields hold a number.
        Assert.NotNull(state.GetValue("a"));
        Assert.NotNull(state.GetValue("b"));
    }

    [Fact]
    public void The_untyped_builder_can_read_siblings_relative_to_the_owning_object()
    {
        var form = BlazorFormBuilder.Create()
            .Number("width")
            .Number("height")
            .Number("area", f => f.Computed(
                ctx => Convert.ToDouble(ctx.Sibling("width") ?? 0d) * Convert.ToDouble(ctx.Sibling("height") ?? 0d),
                "width", "height"))
            .Build();

        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());

        state.SetValue("width", 3d);
        state.SetValue("height", 4d);

        Assert.Equal(12d, state.GetValue("area"));
    }
}
