using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace MttTracker.Api;

/// <summary>
/// Lets the client check that the caller is the allow-listed user before it
/// shows a feature that runs entirely in the browser (the ICM calculator) and
/// so never calls an endpoint that would enforce the allowlist itself.
/// </summary>
public class AccessFunctions(RequestAuthorizer authorizer)
{
    [Function("CheckAccess")]
    public IActionResult CheckAccess(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "access")] HttpRequest req)
    {
        return authorizer.Authorize(req, out _) switch
        {
            RequestAuthorizer.Result.Ok => new NoContentResult(),
            RequestAuthorizer.Result.Unauthenticated => new UnauthorizedResult(),
            _ => new ObjectResult("Forbidden") { StatusCode = StatusCodes.Status403Forbidden },
        };
    }
}
