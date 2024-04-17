using NetBricks;

public class Config : IConfig
{
    private readonly NetBricks.IConfig config;

    public Config(NetBricks.IConfig config)
    {
        this.config = config;
    }

    public static int PORT { get => NetBricks.Config.GetOnce("PORT").AsInt(() => 5000); }

    public static int CACHE_SIZE_IN_MB { get => NetBricks.Config.GetOnce("CACHE_SIZE_IN_MB").AsInt(() => 0); }

    public void Validate()
    {
        this.config.Optional("PORT", PORT);
        this.config.Optional("CACHE_SIZE_IN_MB", CACHE_SIZE_IN_MB);
    }
}