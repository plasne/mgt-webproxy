using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetBricks;
using dotenv.net;
using Microsoft.AspNetCore.Hosting;

DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();

builder.Services.AddSingleLineConsoleLogger();
builder.Services.AddConfig();
builder.Services.AddSingleton<IConfig, Config>();
builder.Services.AddHostedService<LifecycleService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// caching require authentication services as well
if (Config.CACHE_SIZE_IN_MB > 0)
{
    builder.Services.AddSingleton<ICache, InMemoryCache>();
    builder.Services.AddSingleton<ITokenValidator, AzureAdTokenValidator>();
    builder.Services
        .AddAuthentication("default")
        .AddScheme<AuthOptions, AuthHandler>("multi-auth", o => { });
}

builder.Services.AddSingleton<ICredentials, EnvironmentVariableCredentials>();
builder.Services.AddHttpClient();
builder.Services.AddControllers();

// listen (disable TLS)
builder.WebHost.UseKestrel(options =>
{
    options.ListenAnyIP(Config.PORT);
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseStaticFiles();

app.UseRouting();

app.UseMiddleware<OboMiddleware>();
app.MapControllers();

app.Run();
