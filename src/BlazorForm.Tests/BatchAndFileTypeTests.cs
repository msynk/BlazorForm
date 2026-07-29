using Microsoft.AspNetCore.Components.Forms;

namespace BlazorForm.Tests;

public class UploadModel
{
    public string Name { get; set; } = "";
    public IBrowserFile? Resume { get; set; }
    public List<IBrowserFile> Attachments { get; set; } = [];
    public byte[]? Thumbnail { get; set; }
    public Stream? Archive { get; set; }
}

/// <summary>
/// A file property has to resolve to a file field. Falling through to the bottom of the type resolver
/// produced a text box, which is unusable — and silently so, since the schema still rendered.
/// </summary>
public class FileTypeResolutionTests
{
    [Fact]
    public void A_browser_file_property_generates_a_file_field()
    {
        var form = BlazorFormSchemaGenerator.Generate<UploadModel>();

        var resume = form.FindField("Resume")!;
        Assert.Equal(BlazorFormFieldType.File, resume.Type);
        Assert.False(resume.Multiple);
    }

    [Fact]
    public void A_collection_of_files_is_one_multi_file_field_not_a_repeater()
    {
        var attachments = BlazorFormSchemaGenerator.Generate<UploadModel>().FindField("Attachments")!;

        Assert.Equal(BlazorFormFieldType.File, attachments.Type);
        Assert.True(attachments.Multiple);
        Assert.Null(attachments.ItemTemplate);
    }

    [Fact]
    public void Byte_arrays_and_streams_are_uploads_too()
    {
        var form = BlazorFormSchemaGenerator.Generate<UploadModel>();

        Assert.Equal(BlazorFormFieldType.File, form.FindField("Thumbnail")!.Type);
        Assert.False(form.FindField("Thumbnail")!.Multiple);
        Assert.Equal(BlazorFormFieldType.File, form.FindField("Archive")!.Type);
    }

    [Fact]
    public void An_ordinary_collection_is_still_a_repeater()
    {
        var items = BlazorFormSchemaGenerator.Generate<RegistrationModel>().FindField("Items")!;

        Assert.Equal(BlazorFormFieldType.Array, items.Type);
        Assert.NotNull(items.ItemTemplate);
    }
}

/// <summary>
/// Prefilling a form from a saved record is one change, not thirty. Notifying per field re-renders the
/// whole form each time and briefly exposes a half-applied state to anything watching.
/// </summary>
public class BatchUpdateTests
{
    private static (BlazorFormState State, int Notifications) Count(Action<BlazorFormState> act)
    {
        var form = BlazorFormBuilder.Create().Text("a").Text("b").Text("c").Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());

        var notifications = 0;
        state.StateChanged += () => notifications++;
        act(state);
        return (state, notifications);
    }

    [Fact]
    public void Writing_one_at_a_time_notifies_once_per_field()
    {
        var (_, notifications) = Count(s =>
        {
            s.SetValue("a", "1");
            s.SetValue("b", "2");
            s.SetValue("c", "3");
        });

        Assert.Equal(3, notifications);
    }

    [Fact]
    public void SetValues_notifies_once_for_the_whole_batch()
    {
        var (state, notifications) = Count(s => s.SetValues(new Dictionary<string, object?>
        {
            ["a"] = "1", ["b"] = "2", ["c"] = "3"
        }));

        Assert.Equal(1, notifications);
        Assert.Equal("1", state.GetValue("a"));
        Assert.Equal("3", state.GetValue("c"));
    }

    [Fact]
    public void A_prefill_does_not_mark_fields_touched()
    {
        // The user has not visited these fields, so they must not open covered in errors.
        var (state, _) = Count(s => s.SetValues(new Dictionary<string, object?> { ["a"] = "1" }));

        Assert.False(state.IsTouched("a"));
        Assert.True(state.IsDirty("a"));
    }

    [Fact]
    public void Nested_batches_still_notify_exactly_once()
    {
        var (_, notifications) = Count(s => s.Batch(() =>
        {
            s.SetValue("a", "1");
            s.Batch(() => s.SetValue("b", "2"));
            s.SetValue("c", "3");
        }));

        Assert.Equal(1, notifications);
    }

    [Fact]
    public void A_batch_that_throws_still_releases_the_notifications()
    {
        var form = BlazorFormBuilder.Create().Text("a").Build();
        var state = new BlazorFormState(form, new BlazorFormDictionaryDataAccessor());
        var notifications = 0;
        state.StateChanged += () => notifications++;

        Assert.Throws<InvalidOperationException>(() => state.Batch(() =>
        {
            state.SetValue("a", "1");
            throw new InvalidOperationException("boom");
        }));

        // The pending notification is released, and the form is not left permanently silent.
        Assert.Equal(1, notifications);
        state.SetValue("a", "2");
        Assert.Equal(2, notifications);
    }
}
