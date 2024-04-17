using NetBricks;

public class Config : IConfig
{
    private readonly NetBricks.IConfig config;

    public Config(NetBricks.IConfig config)
    {
        this.config = config;
        this.CLIENT_ID = config.Get<string>("CLIENT_ID");
        this.CLIENT_SECRET = config.Get<string>("CLIENT_SECRET");
    }

    public static int PORT { get => NetBricks.Config.GetOnce("PORT").AsInt(() => 5000); }

    public string CLIENT_ID { get; }

    public string CLIENT_SECRET { get; }

    public static int CACHE_SIZE_IN_MB { get => NetBricks.Config.GetOnce("CACHE_SIZE_IN_MB").AsInt(() => 0); }

    public void Validate()
    {
        this.config.Optional("PORT", PORT);
        this.config.Require("CLIENT_ID", this.CLIENT_ID);
        this.config.Require("CLIENT_SECRET", this.CLIENT_SECRET, hideValue: true);
        this.config.Optional("CACHE_SIZE_IN_MB", CACHE_SIZE_IN_MB);
    }
}