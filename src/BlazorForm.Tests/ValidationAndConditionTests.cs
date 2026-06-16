using BlazorForm.Core.Building;
using BlazorForm.Core.Data;
using BlazorForm.Core.Generation;
using BlazorForm.Core.Schema;
using BlazorForm.Core.Validation;

namespace BlazorForm.Tests;

public class ConditionTests
{
    private static IFormDataReader Data(params (string, object?)[] values)
    {
        var d = new DictionaryDataAccessor();
        foreach (var (k, v) in values) d.SetValue(k, v);
        return d;
    }

    [Theory]
    [InlineData(ConditionOperator.Equals, "Business", true)]
    [InlineData(ConditionOperator.NotEquals, "Business", false)]
    [InlineData(ConditionOperator.In, new[] { "Business", "Personal" }, true)]
    public void Evaluates_field_conditions(ConditionOperator op, object value, bool expected)
    {
        var cond = new FieldCondition("AccountType", op, value);
        Assert.Equal(expected, cond.Evaluate(Data(("AccountType", "Business"))));
    }

    [Fact]
    public void Numeric_comparison_handles_mixed_types()
    {
        var cond = new FieldCondition("Age", ConditionOperator.GreaterThanOrEqual, 18);
        Assert.True(cond.Evaluate(Data(("Age", 21))));
        Assert.False(cond.Evaluate(Data(("Age", 16))));
    }

    [Fact]
    public void Condition_group_combines_with_and_or()
    {
        var data = Data(("AccountType", "Business"), ("Age", 30));
        var all = ConditionGroup.All(
            new FieldCondition("AccountType", ConditionOperator.Equals, "Business"),
            new FieldCondition("Age", ConditionOperator.GreaterThan, 18));
        var any = ConditionGroup.Any(
            new FieldCondition("AccountType", ConditionOperator.Equals, "Nope"),
            new FieldCondition("Age", ConditionOperator.GreaterThan, 18));
        Assert.True(all.Evaluate(data));
        Assert.True(any.Evaluate(data));
    }
}

public class BuiltInRuleTests
{
    private static ValidationContext Ctx(object? value)
        => new("f", value, new DictionaryDataAccessor());

    [Fact]
    public async Task Required_fails_on_empty()
    {
        Assert.False((await new RequiredRule().ValidateAsync(Ctx(""))).IsValid);
        Assert.True((await new RequiredRule().ValidateAsync(Ctx("x"))).IsValid);
    }

    [Fact]
    public async Task Range_enforces_bounds()
    {
        var rule = new RangeRule(1, 10);
        Assert.False((await rule.ValidateAsync(Ctx(0))).IsValid);
        Assert.True((await rule.ValidateAsync(Ctx(5))).IsValid);
        Assert.False((await rule.ValidateAsync(Ctx(11))).IsValid);
    }

    [Fact]
    public async Task Email_rule_validates_format()
    {
        var rule = new EmailRule();
        Assert.True((await rule.ValidateAsync(Ctx("a@b.com"))).IsValid);
        Assert.False((await rule.ValidateAsync(Ctx("nope"))).IsValid);
    }
}

public class FormValidatorTests
{
    [Fact]
    public async Task Validates_nested_objects_and_arrays()
    {
        var form = SchemaGenerator.Generate<RegistrationModel>();
        var model = new RegistrationModel { FirstName = "", Email = "bad", Age = 5 };
        model.Items.Add(new LineItem { Product = "", Quantity = 0 });

        var messages = await new FormValidator().ValidateAsync(form, new ModelDataAccessor(model));

        Assert.Contains(messages, m => m.FieldPath == "FirstName");
        Assert.Contains(messages, m => m.FieldPath == "Email");
        Assert.Contains(messages, m => m.FieldPath == "Age");
        Assert.Contains(messages, m => m.FieldPath == "Items[0].Product");
        Assert.Contains(messages, m => m.FieldPath == "Items[0].Quantity");
    }

    [Fact]
    public async Task Hidden_fields_are_not_validated()
    {
        var form = FormBuilder.For<RegistrationModel>()
            .Field(x => x.CompanyName, f => f
                .Required("Company required")
                .VisibleWhen("AccountType", ConditionOperator.Equals, "Business"))
            .Build();

        var model = new RegistrationModel { AccountType = AccountType.Personal };
        var messages = await new FormValidator().ValidateAsync(form, new ModelDataAccessor(model));

        Assert.DoesNotContain(messages, m => m.FieldPath == "CompanyName");
    }
}
