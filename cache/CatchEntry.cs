using System;

public class CacheEntry(string oboToken, string origAud, DateTimeOffset expiry)
{
    public string OboToken { get; } = oboToken;
    public string OrigAud { get; } = origAud;
    public DateTimeOffset Expiry { get; } = expiry;

    public int Length { get => this.OboToken.Length + this.OrigAud.Length; }
}