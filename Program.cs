using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetBricks;
using dotenv.net;
using Microsoft.AspNetCore.Hosting;
using Azure.Identity;

// load .env
DotEnv.Load();

// create the builder
var builder = WebApplication.CreateBuilder(args);

// setup only the single line console logger
builder.Logging.ClearProviders();
builder.Services.AddSingleLineConsoleLogger();

// setup config
builder.Services.AddConfig();
builder.Services.AddSingleton<IConfig, Config>();
builder.Services.AddHostedService<LifecycleService>();

// add swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// add all caching services
if (Config.CACHE_SIZE_IN_MB > 0)
{
    builder.Services.AddSingleton<ICache, InMemoryCache>();
    builder.Services.AddSingleton<ITokenValidator, AzureAdTokenValidator>();
    builder.Services
        .AddAuthentication("default")
        .AddScheme<AuthOptions, AuthHandler>("multi-auth", o => { });
}

// add appropriate credential service
builder.Services.AddDefaultAzureCredential();
builder.Services.AddSingleton<ICredentials>(provider =>
{
    var config = provider.GetService<IConfig>();
    if (string.IsNullOrEmpty(config!.KEYVAULT_URL))
    {
        return new EnvironmentVariableCredentials();
    }
    else
    {
        var dftAzure = provider.GetService<DefaultAzureCredential>();
        var logger = provider.GetService<ILogger<KeyVaultCredentials>>();
        return new KeyVaultCredentials(config, dftAzure!, logger!);
    }
});

// add controllers
builder.Services.AddHttpClient();
builder.Services.AddControllers();

// listen (disable TLS)
builder.WebHost.UseKestrel(options =>
{
    options.ListenAnyIP(Config.PORT);
});

// build
var app = builder.Build();

// use swagger
app.UseSwagger();
app.UseSwaggerUI();

// host the web site if appropriate
if (Config.HOST_TEST_SITE)
{
    app.UseStaticFiles();
}

// use routing
app.UseRouting();

// setup middleware and controllers
app.UseMiddleware<OboMiddleware>();
app.MapControllers();

// run
app.Run();
