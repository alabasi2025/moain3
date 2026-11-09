namespace Hrms.Api.Security;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "hrms";
    public string Audience { get; set; } = "hrms";
    public string Key { get; set; } = "";
    public int AccessMinutes { get; set; } = 30;
    public int RefreshDays { get; set; } = 14;
}
