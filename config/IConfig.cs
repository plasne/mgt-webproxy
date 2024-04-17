public interface IConfig
{
    public string[] INCLUDE_CREDENTIAL_TYPES { get; }

    public string KEYVAULT_URL { get; }

    void Validate();
}