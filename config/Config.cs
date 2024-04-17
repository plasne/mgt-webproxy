using NetBricks;

public class Config : IConfig
{
    private readonly NetBricks.IConfig config;

    public Config(NetBricks.IConfig config)
    {
        this.config = config;
        this.INCLUDE_CREDENTIAL_TYPES = config.Get<string>("INCLUDE_CREDENTIAL_TYPES").AsArray(() => []);
        this.KEYVAULT_URL = config.Get<string>("KEYVAULT_URL");
        this.SCOPE = config.Get<string>("SCOPE").AsString(() => "https://graph.microsoft.com/chat.read");
    }

    public static string ASPNETCORE_ENVIRONMENT { get => NetBricks.Config.GetOnce("ASPNETCORE_ENVIRONMENT"); }

    public static int PORT { get => NetBricks.Config.GetOnce("PORT").AsInt(() => 5000); }

    public static int CACHE_SIZE_IN_MB { get => NetBricks.Config.GetOnce("CACHE_SIZE_IN_MB").AsInt(() => 0); }

    public string[] INCLUDE_CREDENTIAL_TYPES { get; }

    public string KEYVAULT_URL { get; }

    public string SCOPE { get; }

    public void Validate()
    {
        this.config.Optional("ASPNETCORE_ENVIRONMENT", ASPNETCORE_ENVIRONMENT);
        this.config.Optional("PORT", PORT);
        this.config.Optional("CACHE_SIZE_IN_MB", CACHE_SIZE_IN_MB);
        this.config.Optional("INCLUDE_CREDENTIAL_TYPES", this.INCLUDE_CREDENTIAL_TYPES);
        this.config.Optional("KEYVAULT_URL", this.KEYVAULT_URL);
        this.config.Require("SCOPE", this.SCOPE);
    }
}