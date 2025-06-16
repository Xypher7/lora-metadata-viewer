using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Text;

namespace SwarmUI.Extensions.LoRAViewer // Assuming a namespace for the extension
{
    public static class SafetensorsParser
    {
        public static JObject ParseSafetensorsMetadata(byte[] fileBytes)
        {
            if (fileBytes == null)
            {
                // Or throw new ArgumentNullException(nameof(fileBytes));
                Console.Error.WriteLine("[SafetensorsParser] Error: fileBytes is null.");
                return null;
            }

            if (fileBytes.Length < 8)
            {
                // Log error or throw: File is too short
                Console.Error.WriteLine($"[SafetensorsParser] Error: File is too short. Length: {fileBytes.Length} bytes. Expected at least 8 bytes for metadata size.");
                return null;
            }

            ulong metadataSize;
            try
            {
                metadataSize = BitConverter.ToUInt64(fileBytes, 0);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                // This might happen if fileBytes.Length is < 8, though we check above. Redundant but safe.
                Console.Error.WriteLine($"[SafetensorsParser] Error reading metadata size: {ex.Message}");
                return null;
            }

            // Basic sanity check for metadataSize, e.g., not larger than the file itself minus header, or some reasonable maximum.
            // Max metadata typically a few MBs. Let's cap at 50MB for sanity, to prevent huge allocations.
            const ulong MAX_REASONABLE_METADATA_SIZE = 50 * 1024 * 1024;
            if (metadataSize == 0)
            {
                 Console.Error.WriteLine("[SafetensorsParser] Error: Reported metadata size is 0.");
                 return null;
            }
            if (metadataSize > MAX_REASONABLE_METADATA_SIZE)
            {
                Console.Error.WriteLine($"[SafetensorsParser] Error: Reported metadata size ({metadataSize} bytes) is unreasonably large.");
                return null;
            }

            if ((ulong)fileBytes.Length < 8 + metadataSize)
            {
                Console.Error.WriteLine($"[SafetensorsParser] Error: File is incomplete. Expected {8 + metadataSize} bytes, but got {fileBytes.Length} bytes based on metadata size.");
                return null;
            }

            string metadataJsonString;
            try
            {
                metadataJsonString = Encoding.UTF8.GetString(fileBytes, 8, (int)metadataSize);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                 Console.Error.WriteLine($"[SafetensorsParser] Error extracting metadata string: {ex.Message}. MetadataSize: {metadataSize}, FileLength: {fileBytes.Length}");
                 return null;
            }
            catch (Exception ex) // Other potential exceptions from GetString
            {
                Console.Error.WriteLine($"[SafetensorsParser] Unexpected error decoding metadata string: {ex.ToString()}");
                return null;
            }


            JObject fullMetadata;
            try
            {
                fullMetadata = JObject.Parse(metadataJsonString);
            }
            catch (JsonReaderException ex)
            {
                Console.Error.WriteLine($"[SafetensorsParser] Error parsing metadata JSON: {ex.Message}. JSON string was: {metadataJsonString.Substring(0, Math.Min(metadataJsonString.Length, 200))}"); // Log a snippet
                return null;
            }
            catch (Exception ex) // Other potential exceptions from JObject.Parse
            {
                Console.Error.WriteLine($"[SafetensorsParser] Unexpected error parsing JSON: {ex.ToString()}");
                return null;
            }

            if (fullMetadata.TryGetValue("__metadata__", StringComparison.OrdinalIgnoreCase, out JToken metadataToken))
            {
                if (metadataToken is JObject metadataObject)
                {
                    return metadataObject;
                }
                else
                {
                    Console.Error.WriteLine("[SafetensorsParser] Error: '__metadata__' field is not a JSON object.");
                    // Optionally, could return a JObject indicating this error, or a JObject containing the non-object token if that's useful.
                    // For now, strict parsing: expect it to be an object.
                    return null;
                }
            }
            else
            {
                Console.Error.WriteLine("[SafetensorsParser] Error: '__metadata__' field not found in JSON.");
                // This could be a valid safetensors file but not a LoRA, or just missing the field.
                // Depending on requirements, we might return an empty JObject or a specific status.
                // For now, indicates an issue for LoRA metadata viewing.
                return null;
            }
        }
    }
}
