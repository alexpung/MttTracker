using System.ComponentModel.DataAnnotations;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

using MttTracker.Shared;

namespace MttTracker.Api;

/// <summary>
/// CRUD endpoints for tournament entries. Every operation is scoped to the
/// authenticated, allow-listed user resolved by <see cref="RequestAuthorizer"/>.
/// </summary>
public class EntriesFunctions(
    CosmosEntryStore store,
    RequestAuthorizer authorizer,
    JsonSerializerOptions jsonOptions,
    ILogger<EntriesFunctions> logger)
{
    [Function("GetEntries")]
    public async Task<IActionResult> GetEntries(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "entries")] HttpRequest req)
    {
        if (Gate(req, out var userId) is { } denied)
        {
            return denied;
        }

        var entries = await store.ListAsync(userId);
        return new OkObjectResult(entries);
    }

    [Function("CreateEntry")]
    public async Task<IActionResult> CreateEntry(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "entries")] HttpRequest req)
    {
        if (Gate(req, out var userId) is { } denied)
        {
            return denied;
        }

        var entry = await ReadBodyAsync(req);
        if (entry is null)
        {
            return new BadRequestObjectResult("Invalid request body.");
        }
        if (!TryValidate(entry, out var error))
        {
            return new BadRequestObjectResult(error);
        }
        if (!Normalize(entry, out var currencyError))
        {
            return new BadRequestObjectResult(currencyError);
        }

        entry.UserId = userId;
        var created = await store.CreateAsync(entry);
        return new OkObjectResult(created);
    }

    [Function("UpdateEntry")]
    public async Task<IActionResult> UpdateEntry(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "entries/{id}")] HttpRequest req,
        string id)
    {
        if (Gate(req, out var userId) is { } denied)
        {
            return denied;
        }

        var entry = await ReadBodyAsync(req);
        if (entry is null)
        {
            return new BadRequestObjectResult("Invalid request body.");
        }
        if (!TryValidate(entry, out var error))
        {
            return new BadRequestObjectResult(error);
        }
        if (!Normalize(entry, out var currencyError))
        {
            return new BadRequestObjectResult(currencyError);
        }

        // Only update a record the caller already owns.
        var existing = await store.GetAsync(userId, id);
        if (existing is null)
        {
            return new NotFoundResult();
        }

        entry.Id = id;
        entry.UserId = userId;
        var updated = await store.ReplaceAsync(entry);
        return new OkObjectResult(updated);
    }

    [Function("DeleteEntry")]
    public async Task<IActionResult> DeleteEntry(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "entries/{id}")] HttpRequest req,
        string id)
    {
        if (Gate(req, out var userId) is { } denied)
        {
            return denied;
        }

        await store.DeleteAsync(userId, id);
        return new NoContentResult();
    }

    /// <summary>Authorize the request; returns an error result to short-circuit, or null on success.</summary>
    private IActionResult? Gate(HttpRequest req, out string userId)
    {
        return authorizer.Authorize(req, out userId) switch
        {
            RequestAuthorizer.Result.Ok => null,
            RequestAuthorizer.Result.Unauthenticated => new UnauthorizedResult(),
            _ => new ObjectResult("Forbidden") { StatusCode = StatusCodes.Status403Forbidden },
        };
    }

    private async Task<TournamentEntry?> ReadBodyAsync(HttpRequest req)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<TournamentEntry>(req.Body, jsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse request body.");
            return null;
        }
    }

    private static bool TryValidate(TournamentEntry entry, out string? error)
    {
        var context = new ValidationContext(entry);
        var results = new List<ValidationResult>();
        if (Validator.TryValidateObject(entry, context, results, validateAllProperties: true))
        {
            error = null;
            return true;
        }

        error = string.Join("; ", results.Select(r => r.ErrorMessage));
        return false;
    }

    /// <summary>
    /// Canonicalizes the currency code and enforces invariants the client could
    /// otherwise get wrong: an unknown currency is rejected, and a home-currency
    /// entry always has a rate of exactly 1.
    /// </summary>
    private static bool Normalize(TournamentEntry entry, out string? error)
    {
        entry.Currency = (entry.Currency ?? Currencies.Home).Trim().ToUpperInvariant();
        if (!Currencies.IsSupported(entry.Currency))
        {
            error = $"Unsupported currency '{entry.Currency}'.";
            return false;
        }
        if (entry.IsHomeCurrency)
        {
            entry.ExchangeRate = 1m;
        }
        error = null;
        return true;
    }
}
