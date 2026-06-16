using FluentValidation;
using BlazorForm;
using BlazorForm.Demo.Components;
using BlazorForm.Demo.Forms;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// BlazorForm rendering services. Register a custom renderer ("rating") to demonstrate extensibility.
builder.Services.AddBlazorForm(registry =>
{
    registry.RegisterCustom("rating", typeof(BlazorForm.Demo.Components.Renderers.StarRatingInput));
});

// FluentValidation validators used by the FluentValidation demo.
builder.Services.AddScoped<IValidator<JobApplication>, JobApplicationValidator>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
