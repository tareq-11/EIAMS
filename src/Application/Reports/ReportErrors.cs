using SharedKernel;

namespace Application.Reports;

public static class ReportErrors
{
    public static readonly Error Forbidden = Error.Forbidden(
        "Reports.Forbidden",
        "The current user cannot view this report in any warehouse.");
}
