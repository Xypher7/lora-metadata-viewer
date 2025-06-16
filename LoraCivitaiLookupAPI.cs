using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json; // For JsonConvert
using Newtonsoft.Json.Linq;
using SwarmUI.Core;
using SwarmUI.WebAPI;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SwarmUI.Extensions.LoRAViewer
{
    // LoraIdentifierRequest DTO is already defined in LoraMetadataAPI.cs,
    // assuming they are in the same compilation unit or LoraIdentifierRequest is made more accessible.
    // If not, it should be defined here or in a shared file.
    // For this operation, let's assume it's accessible.

    public class LoraCivitaiLookupAPI : APIHandler
    {
        public override string Path => "/API/LoraCivitaiLookup";

        public static void Register()
        {
            API.RegisterAPICall(new LoraCivitaiLookupAPI());
            Logs.Info("[LoraCivitaiLookupAPI] Registered API endpoint at /API/LoraCivitaiLookup (now expects LoraIdentifier).");
        }

        public override async Task<JObject> Handle(HttpContext context)
        {
            if (context.Request.Method != "POST")
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                return new JObject { ["error"] = "Only POST requests are allowed." };
            }

            if (context.Request.ContentType == null || !context.Request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                return new JObject { ["error"] = "Invalid Content-Type. Expected 'application/json'." };
            }

            string loraIdentifier = null;
            try
            {
                string requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
                LoraIdentifierRequest requestPayload = JsonConvert.DeserializeObject<LoraIdentifierRequest>(requestBody);
                loraIdentifier = requestPayload?.LoraIdentifier;

                if (string.IsNullOrWhiteSpace(loraIdentifier))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return new JObject { ["error"] = "LoraIdentifier is missing in the request body." };
                }

                Logs.Info($"[LoraCivitaiLookupAPI] Received request for LoraIdentifier: {loraIdentifier}");

                if (!FileAccessHelper.TryResolveSecurePath(loraIdentifier, out string absolutePath, out string errorMessage))
                {
                    if (errorMessage.StartsWith("Access denied"))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    }
                    else if (errorMessage.Contains("not found"))
                    {
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                    }
                    else
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    }
                    return new JObject { ["error"] = errorMessage };
                }

                byte[] fileBytes = await File.ReadAllBytesAsync(absolutePath);

                // Optional: File size check, similar to LoraMetadataAPI if needed.

                // Try AutoV2 hash first
                string autoV2Hash = await CivitaiUtils.CalculateFileHashAsync(fileBytes, false);
                JObject civitaiData = null;
                string hashUsed = null;
                string successfulHashValue = null;

                if (!string.IsNullOrWhiteSpace(autoV2Hash))
                {
                    Logs.Info($"[LoraCivitaiLookupAPI] Calculated AutoV2 hash: {autoV2Hash}");
                    civitaiData = await CivitaiUtils.GetCivitaiInfoByHashAsync(autoV2Hash);
                    if (civitaiData != null)
                    {
                        hashUsed = "AutoV2";
                        successfulHashValue = autoV2Hash;
                        Logs.Info($"[LoraCivitaiLookupAPI] Found Civitai info using AutoV2 hash: {autoV2Hash}");
                    }
                }

                if (civitaiData == null)
                {
                    Logs.Info($"[LoraCivitaiLookupAPI] No Civitai info with AutoV2 hash for '{loraIdentifier}'. Trying AutoV3.");
                    string autoV3Hash = await CivitaiUtils.CalculateFileHashAsync(fileBytes, true);
                    if (!string.IsNullOrWhiteSpace(autoV3Hash))
                    {
                        Logs.Info($"[LoraCivitaiLookupAPI] Calculated AutoV3 hash: {autoV3Hash}");
                        civitaiData = await CivitaiUtils.GetCivitaiInfoByHashAsync(autoV3Hash);
                        if (civitaiData != null)
                        {
                            hashUsed = "AutoV3";
                            successfulHashValue = autoV3Hash;
                            Logs.Info($"[LoraCivitaiLookupAPI] Found Civitai info using AutoV3 hash for '{loraIdentifier}': {autoV3Hash}");
                        }
                        else
                        {
                            Logs.Info($"[LoraCivitaiLookupAPI] No Civitai info found with AutoV3 hash for '{loraIdentifier}' either.");
                        }
                    }
                    else
                    {
                        Logs.Warning($"[LoraCivitaiLookupAPI] Failed to calculate AutoV3 hash for '{loraIdentifier}'.");
                    }
                }

                if (civitaiData != null)
                {
                    context.Response.StatusCode = StatusCodes.Status200OK;
                    return new JObject
                    {
                        ["found"] = true,
                        ["hashUsed"] = hashUsed,
                        ["hashValue"] = successfulHashValue,
                        ["data"] = civitaiData
                    };
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status200OK; // Still 200 OK, but indicates not found
                    return new JObject { ["found"] = false };
                }
            }
            catch (JsonException ex) // Error deserializing the request body
            {
                Logs.Warning($"[LoraCivitaiLookupAPI] JSON deserialization error: {ex.Message}");
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return new JObject { ["error"] = "Invalid JSON request body.", ["detail"] = ex.Message };
            }
            catch (Exception ex)
            {
                Logs.Error($"[LoraCivitaiLookupAPI] Error processing LoraIdentifier '{loraIdentifier}': {ex.ToString()}");
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return new JObject { ["error"] = "An unexpected error occurred on the server during Civitai lookup.", ["detail"] = ex.Message };
            }
        }
    }
}
