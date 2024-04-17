public interface IConfig
{
    string CLIENT_ID { get; }
    string CLIENT_SECRET { get; }

    void Validate();
}