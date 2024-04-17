using System.IdentityModel.Tokens.Jwt;

public static class Ext
{
    public static bool TryGetString(this JwtPayload payload, string key, out string value)
    {
        value = string.Empty;

        if (!payload.TryGetValue(key, out var element))
        {
            return false;
        }

        string? possible = element.ToString();
        if (string.IsNullOrEmpty(possible))
        {
            return false;
        }

        value = possible;
        return true;
    }

    public static bool TryGetInt(this JwtPayload payload, string key, out int value)
    {
        value = 0;

        if (!payload.TryGetValue(key, out var element))
        {
            return false;
        }

        if (!int.TryParse(element.ToString(), out int possible))
        {
            return false;
        }

        value = possible;
        return true;
    }

}