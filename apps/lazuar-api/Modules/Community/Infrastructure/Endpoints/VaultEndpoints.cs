// apps/lazuar-api/Modules/Community/Infrastructure/Endpoints/VaultEndpoints.cs
using System;
using System.IO;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace Modules.Community.Infrastructure;

public static class VaultEndpoints
{
    public static RouteGroupBuilder MapVaultEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/vault/upload", async Task<Results<Ok<object>, BadRequest<string>>> (
            IFormFile file,
            IExecutionContextAccessor ctx,
            IR2StorageService r2Service,
            IConfiguration config) =>
        {
            if (file == null || file.Length == 0)
            {
                return TypedResults.BadRequest("No file uploaded.");
            }

            var tenantId = ctx.TenantId;
            var bucket = config["R2_BUCKET_NAME"] ?? "lazuar-vault-test";
            var publicUrlBase = config["R2_PUBLIC_DEV_URL"]?.TrimEnd('/');

            var extension = Path.GetExtension(file.FileName);
            var key = $"vault/{tenantId}/{Guid.CreateVersion7()}{extension}";

            using var stream = file.OpenReadStream();
            var resultKey = await r2Service.UploadAsync(stream, bucket, key, file.ContentType);

            if (resultKey == null)
            {
                return TypedResults.BadRequest("Failed to upload file to storage.");
            }

            var url = $"{publicUrlBase}/{resultKey}";

            return TypedResults.Ok<object>(new { url });
        }).DisableAntiforgery();

        return group;
    }
}
