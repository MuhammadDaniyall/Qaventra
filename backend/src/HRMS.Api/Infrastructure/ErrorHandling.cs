using HRMS.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Infrastructure;

/// <summary>Turns business-rule, duplicate-key and foreign-key failures into ProblemDetails; everything else stays a 500.</summary>
public class ErrorHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    /// <summary>The status a handled exception is answered with, or 0 when it is left to become a 500.</summary>
    public static int StatusFor(Exception exception) => exception switch
    {
        DomainException => StatusCodes.Status400BadRequest,
        NotFoundException => StatusCodes.Status404NotFound,
        ForbiddenException => StatusCodes.Status403Forbidden,
        DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } => StatusCodes.Status409Conflict,
        // Foreign-key conflict: deleting something still referenced, or linking to something just deleted.
        DbUpdateException { InnerException: SqlException { Number: 547 } } => StatusCodes.Status409Conflict,
        _ => 0,
    };

    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var status = StatusFor(exception);
        if (status == 0)
            return false;
        var title = exception switch
        {
            DbUpdateException { InnerException: SqlException { Number: 547 } sql } => sql.Message.Contains("REFERENCE constraint", StringComparison.OrdinalIgnoreCase)
                ? "This is still used elsewhere. Remove those links or turn it off instead."
                : "Something this refers to no longer exists. Refresh and try again.",
            _ when status == StatusCodes.Status409Conflict => "A record with the same unique value already exists.",
            _ => exception.Message,
        };

        http.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails { Status = status, Title = title },
            Exception = exception,
        });
    }
}

public class NotFoundException(string what) : Exception($"{what} not found.");

public class ForbiddenException(string message) : Exception(message);
