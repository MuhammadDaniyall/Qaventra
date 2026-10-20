using HRMS.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Api.Infrastructure;

/// <summary>Turns business-rule and duplicate-key failures into ProblemDetails; everything else stays a 500.</summary>
public class ErrorHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            DomainException e => (StatusCodes.Status400BadRequest, e.Message),
            NotFoundException e => (StatusCodes.Status404NotFound, e.Message),
            ForbiddenException e => (StatusCodes.Status403Forbidden, e.Message),
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } =>
                (StatusCodes.Status409Conflict, "A record with the same unique value already exists."),
            _ => (0, ""),
        };
        if (status == 0)
            return false;

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
