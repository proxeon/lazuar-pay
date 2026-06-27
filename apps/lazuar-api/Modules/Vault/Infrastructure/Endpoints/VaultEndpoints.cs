using System;
using System.IO;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Lazuar.ApiTypes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace Modules.Vault.Infrastructure;

public static class VaultEndpoints
{
    public static RouteGroupBuilder MapVaultEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/presigned-url", Task<Results<Ok<GetPresignedUrlResponseDto>, BadRequest<string>>> (
            [FromBody] GetPresignedUrlRequestDto req,
            IExecutionContextAccessor ctx,
            IR2StorageService r2Service,
            IConfiguration config) =>
        {
            if (string.IsNullOrWhiteSpace(req.File_name))
            {
                return Task.FromResult<Results<Ok<GetPresignedUrlResponseDto>, BadRequest<string>>>(TypedResults.BadRequest("File name is required."));
            }

            var tenantId = ctx.TenantId;
            var bucket = config["R2_BUCKET_NAME"] ?? "lazuar-vault-test";
            var publicUrlBase = config["R2_PUBLIC_DEV_URL"]?.TrimEnd('/');

            var extension = Path.GetExtension(req.File_name);
            var key = $"vault/{tenantId}/{Guid.CreateVersion7()}{extension}";

            var uploadUrl = r2Service.GetPresignedUploadUrl(bucket, key, req.Content_type);
            var finalUrl = $"{publicUrlBase}/{key}";

            return Task.FromResult<Results<Ok<GetPresignedUrlResponseDto>, BadRequest<string>>>(TypedResults.Ok(new GetPresignedUrlResponseDto 
            { 
                Upload_url = uploadUrl, 
                Final_url = finalUrl 
            }));
        });

        return group;
    }
}
