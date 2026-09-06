namespace Web.Api.Infrastructure;

public static class RateLimitingPolicies
{
    public const string Authentication = "authentication";
    public const string Reporting = "reporting-concurrency";
    public const string Upload = "upload-concurrency";
    public const string Posting = "posting-concurrency";
}
