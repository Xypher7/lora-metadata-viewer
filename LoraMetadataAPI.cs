using Microsoft.AspNetCore.Http; // Assuming SwarmUI uses ASP.NET Core for HttpContext
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
    // Helper DTO for deserializing the request body
    public class LoraIdentifierRequest
    {
        public string LoraIdentifier { get; set; }
    }

    public class LoraMetadataAPI : APIHandler
    {
        public override string Path => "/API/LoraMetadata";

        public static void Register()
        {
            API.RegisterAPICall(new LoraMetadataAPI());
            Logs.Info("[LoraMetadataAPI] Registered API endpoint at /API/LoraMetadata (now expects LoraIdentifier).");
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

                Logs.Info($"[LoraMetadataAPI] Received request for LoraIdentifier: {loraIdentifier}");

                if (!FileAccessHelper.TryResolveSecurePath(loraIdentifier, out string absolutePath, out string errorMessage))
                {
                    // Determine appropriate status code based on error message
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
                        context.Response.StatusCode = StatusCodes.Status400BadRequest; // Generic bad input
                    }
                    return new JObject { ["error"] = errorMessage };
                }

                byte[] fileBytes = await File.ReadAllBytesAsync(absolutePath);

                // Optional: Check file size here if desired, though TryResolveSecurePath already checked existence.
                // const long MAX_FILE_SIZE = 2L * 1024 * 1024 * 1024;
                // if (new FileInfo(absolutePath).Length > MAX_FILE_SIZE) { ... }


                JObject metadata = SafetensorsParser.ParseSafetensorsMetadata(fileBytes);

                if (metadata == null)
                {
                    context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                    return new JObject { ["error"] = "Failed to parse metadata from the safetensors file. The file might be invalid, not a LoRA, or corrupted." };
                }

                context.Response.StatusCode = StatusCodes.Status200OK;
                return new JObject
                {
                    ["localMetadata"] = metadata,
                    ["loraName"] = System.IO.Path.GetFileNameWithoutExtension(loraIdentifier), // Use System.IO.Path for consistency
                    ["identifierReceived"] = loraIdentifier
                };
            }
            catch (JsonException ex) // Error deserializing the request body
            {
                Logs.Warning($"[LoraMetadataAPI] JSON deserialization error: {ex.Message}");
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return new JObject { ["error"] = "Invalid JSON request body.", ["detail"] = ex.Message };
            }
            catch (Exception ex)
            {
                Logs.Error($"[LoraMetadataAPI] Error processing LoraIdentifier '{loraIdentifier}': {ex.ToString()}");
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return new JObject { ["error"] = "An unexpected error occurred on the server.", ["detail"] = ex.Message };
            }
        }
    }
}
