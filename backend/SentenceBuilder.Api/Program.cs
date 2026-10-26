using SentenceBuilder.Api.Configuration;
using SentenceBuilder.Api.Errors;
using SentenceBuilder.Api.Interfaces;
using SentenceBuilder.Api.Persistence;
using SentenceBuilder.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSentenceBuilderPersistence(builder.Configuration);
builder.Services.AddScoped<IVocabularyRepository, VocabularyRepository>();
builder.Services.AddScoped<ISentenceRepository, SentenceRepository>();
builder.Services.AddScoped<VocabularyService>();
builder.Services.AddScoped<SentenceService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddCors(options => options.AddPolicy(CorsConfiguration.PolicyName, policy =>
    policy.WithOrigins(CorsConfiguration.AllowedOrigins(builder.Configuration))
        .WithMethods("GET", "POST", "PUT").AllowAnyHeader()));

var app = builder.Build();
DatabaseConfiguration.RequiredConnectionString(app.Configuration);
CorsConfiguration.AllowedOrigins(app.Configuration);
app.UseExceptionHandler();
app.UseStatusCodePages();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
if (app.Configuration.GetValue("Https:Redirect", true)) app.UseHttpsRedirection();
app.UseCors(CorsConfiguration.PolicyName);
app.MapControllers();
app.Run();

public partial class Program;
