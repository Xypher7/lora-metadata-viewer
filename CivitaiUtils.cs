using Newtonsoft.Json.Linq;
using SwarmUI.Core; // For Logs
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SwarmUI.Extensions.LoRAViewer
{
    public static class CivitaiUtils
    {
        private static readonly HttpClient _httpClient;

        static CivitaiUtils()
        {
            _httpClient = new HttpClient();
            // It's good practice to set a User-Agent. Replace with actual extension info if possible.
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SwarmUI-LoraMetadataExtension/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(30); // Set a reasonable timeout
        }

        /// <summary>
        /// Calculates the SHA256 hash of the given file bytes.
        /// </summary>
        /// <param name="fileBytes">The bytes of the file.</param>
        /// <param name="autoV3Format">If true, skips the metadata header for hashing (AutoV3 style). Otherwise, hashes the full file (AutoV2 style).</param>
        /// <returns>Lowercase hex string of the hash, or null if an error occurs.</returns>
        public static async Task<string> CalculateFileHashAsync(byte[] fileBytes, bool autoV3Format)
        {
            if (fileBytes == null || fileBytes.Length == 0)
            {
                Logs.Error("[CivitaiUtils.CalculateFileHashAsync] File bytes are null or empty.");
                return null;
            }

            try
            {
                byte[] hashBytes;
                if (autoV3Format)
                {
                    if (fileBytes.Length < 8)
                    {
                        Logs.Error("[CivitaiUtils.CalculateFileHashAsync] File too short for AutoV3 hash (less than 8 bytes for metadata header length).");
                        return null;
                    }
                    ulong metadataSize = BitConverter.ToUInt64(fileBytes, 0);
                    int headerOffset = 8 + (int)metadataSize;

                    if (headerOffset < 0 || (long)headerOffset > fileBytes.Length) // Check for overflow or invalid offset
                    {
                        Logs.Error($"[CivitaiUtils.CalculateFileHashAsync] Invalid header offset {headerOffset} for AutoV3 hash. MetadataSize: {metadataSize}, FileLength: {fileBytes.Length}");
                        return null;
                    }

                    // Use ReadOnlyMemory<byte> for efficient slicing without copying
                    ReadOnlyMemory<byte> dataToHash = new ReadOnlyMemory<byte>(fileBytes, headerOffset, fileBytes.Length - headerOffset);

                    // HashDataAsync can work directly with ReadOnlyMemory<byte> in modern .NET
                    // For older .NET versions, you might need to use a stream wrapper.
                    // Assuming .NET Core or .NET 5+ where this is available.
                    using (SHA256 sha256 = SHA256.Create())
                    {
                        // This is a simplified way if HashDataAsync(ReadOnlyMemory<byte>) isn't available or for clarity with streams
                        using (var stream = new MemoryStream(fileBytes, headerOffset, fileBytes.Length - headerOffset, false))
                        {
                             hashBytes = await sha256.ComputeHashAsync(stream);
                        }
                    }
                }
                else // AutoV2 format
                {
                     using (SHA256 sha256 = SHA256.Create())
                     {
                        using (var stream = new MemoryStream(fileBytes, false)) // Hash the full file
                        {
                           hashBytes = await sha256.ComputeHashAsync(stream);
                        }
                     }
                }

                // Convert byte array to lowercase hex string
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
            catch (ArgumentOutOfRangeException ex) // Can be thrown by MemoryStream if offset/count is bad
            {
                Logs.Error($"[CivitaiUtils.CalculateFileHashAsync] Argument error during hashing: {ex.Message}");
                return null;
            }
            catch (ObjectDisposedException ex) // SHA256 disposed prematurely (shouldn't happen with using)
            {
                Logs.Error($"[CivitaiUtils.CalculateFileHashAsync] Object disposed error during hashing: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Logs.Error($"[CivitaiUtils.CalculateFileHashAsync] Unexpected error during hashing: {ex.ToString()}");
                return null;
            }
        }

        /// <summary>
        /// Fetches model version information from Civitai API by its SHA256 hash.
        /// </summary>
        /// <param name="hash">The SHA256 hash of the model version.</param>
        /// <returns>A JObject containing the API response, or null if not found or an error occurs.</returns>
        public static async Task<JObject> GetCivitaiInfoByHashAsync(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                Logs.Warning("[CivitaiUtils.GetCivitaiInfoByHashAsync] Hash is null or empty.");
                return null;
            }

            string apiUrl = $"https://civitai.com/api/v1/model-versions/by-hash/{hash.ToLowerInvariant()}";

            try
            {
                Logs.Info($"[CivitaiUtils.GetCivitaiInfoByHashAsync] Querying Civitai: {apiUrl}");
                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    string jsonContent = await response.Content.ReadAsStringAsync();
                    if (string.IsNullOrWhiteSpace(jsonContent))
                    {
                        Logs.Warning($"[CivitaiUtils.GetCivitaiInfoByHashAsync] Received empty response from Civitai for hash {hash}.");
                        return null;
                    }
                    return JObject.Parse(jsonContent);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Logs.Info($"[CivitaiUtils.GetCivitaiInfoByHashAsync] No model found on Civitai for hash {hash}.");
                    return null;
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Logs.Error($"[CivitaiUtils.GetCivitaiInfoByHashAsync] Civitai API request failed for hash {hash}. Status: {response.StatusCode}. Response: {errorContent.Substring(0, Math.Min(errorContent.Length, 500))}");
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                Logs.Error($"[CivitaiUtils.GetCivitaiInfoByHashAsync] HTTP request exception for hash {hash}: {ex.Message}");
                return null;
            }
            catch (JsonReaderException ex)
            {
                Logs.Error($"[CivitaiUtils.GetCivitaiInfoByHashAsync] Failed to parse JSON response from Civitai for hash {hash}: {ex.Message}");
                return null;
            }
            catch (TaskCanceledException ex) // Handles timeout
            {
                Logs.Error($"[CivitaiUtils.GetCivitaiInfoByHashAsync] Civitai API request timed out for hash {hash}: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Logs.Error($"[CivitaiUtils.GetCivitaiInfoByHashAsync] Unexpected error fetching data from Civitai for hash {hash}: {ex.ToString()}");
                return null;
            }
        }
    }
}
