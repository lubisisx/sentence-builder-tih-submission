using Microsoft.AspNetCore.Mvc;
using SentenceBuilder.Api.Services;

namespace SentenceBuilder.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult<T> Respond<T>(OperationResult<T> result) => result.Status switch
    {
        200 => Ok(result.Value),
        400 => ValidationProblem(new ValidationProblemDetails(result.Errors!) { Status = 400, Title = "Invalid request." }),
        404 => Problem(statusCode: 404, title: "Resource not found."),
        _ => Problem(statusCode: 500, title: "An unexpected error occurred.")
    };
}
