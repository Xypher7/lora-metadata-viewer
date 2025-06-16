using System;
using System.IO;
using SwarmUI.Core; // For Logs

namespace SwarmUI.Extensions.LoRAViewer
{
    public static class FileAccessHelper
    {
        // ####################################################################################################################
        // ### PLACEHOLDER: LoRA Root Directory                                                                             ###
        // ### This path MUST be configured securely in a real SwarmUI environment.                                         ###
        // ### It should point to the root directory where all LoRA models are stored.                                      ###
        // ### Example for Linux: "/srv/swarmui/lora_models/"                                                               ###
        // ### Example for Windows: "C:/SwarmData/Models/lora/"                                                             ###
        // ###                                                                                                              ###
        // ### >>> IMPORTANT SECURITY NOTE:                                                                                 ###
        // ### NEVER allow this path to be controlled by user input or client-side parameters.                            ###
        // ### It must be a server-side configuration.                                                                      ###
        // ####################################################################################################################
        public static readonly string LoraRootDirectory = "/tmp/lora_files_placeholder/"; // Default placeholder for testing

        /// <summary>
        /// Tries to resolve a relative LoRA identifier to a secure, absolute file path.
        /// </summary>
        /// <param name="relativeIdentifier">The relative path of the LoRA file from the configured LoraRootDirectory.</param>
        /// <param name="resolvedPath">The fully resolved, normalized, and validated absolute path to the LoRA file if successful.</param>
        /// <param name="errorMessage">An error message if path resolution or validation fails.</param>
        /// <returns>True if the path is valid and the file exists; otherwise, false.</returns>
        public static bool TryResolveSecurePath(string relativeIdentifier, out string resolvedPath, out string errorMessage)
        {
            resolvedPath = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(relativeIdentifier))
            {
                errorMessage = "LoRA identifier is missing or empty.";
                Logs.Warning($"[FileAccessHelper] Attempt to resolve empty identifier.");
                return false;
            }

            // Normalize relative path to prevent issues with mixed slashes if necessary, though Path.Combine usually handles this.
            string normalizedRelativeIdentifier = relativeIdentifier.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

            // Prevent escaping the root directory via '..' or absolute paths.
            // Path.IsPathRooted is a good check for absolute paths.
            // Checking for ".." is a common explicit check, although GetFullPath and the StartsWith check below are the main defense.
            if (normalizedRelativeIdentifier.Contains("..") || Path.IsPathRooted(normalizedRelativeIdentifier))
            {
                errorMessage = "Invalid LoRA identifier format: Contains '..' or is an absolute path.";
                Logs.Warning($"[FileAccessHelper] Invalid identifier format: '{relativeIdentifier}'");
                return false;
            }

            string normalizedLoraRoot;
            try
            {
                // Normalize the root directory path once to ensure consistent comparisons
                normalizedLoraRoot = Path.GetFullPath(LoraRootDirectory);
                if (!normalizedLoraRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    normalizedLoraRoot += Path.DirectorySeparatorChar;
                }
            }
            catch (Exception ex)
            {
                Logs.Error($"[FileAccessHelper] Critical error with LoraRootDirectory ('{LoraRootDirectory}'): {ex.Message}");
                errorMessage = "Server configuration error: LoRA root directory is invalid.";
                return false; // This is a server config issue, bail out.
            }


            string combinedPath;
            try
            {
                combinedPath = Path.Combine(normalizedLoraRoot, normalizedRelativeIdentifier);
                resolvedPath = Path.GetFullPath(combinedPath); // Normalizes the path, resolves '.' and '..' if any slip through (though we try to block '..')
            }
            catch (ArgumentException ex) // Invalid characters in path, etc.
            {
                Logs.Warning($"[FileAccessHelper] ArgumentException while combining or normalizing path for identifier '{relativeIdentifier}': {ex.Message}");
                errorMessage = "Invalid LoRA identifier: Contains invalid characters.";
                return false;
            }
            catch (Exception ex) // PathTooLongException, etc.
            {
                Logs.Warning($"[FileAccessHelper] Error combining or normalizing path for identifier '{relativeIdentifier}': {ex.ToString()}");
                errorMessage = "Error resolving LoRA path: " + ex.Message; // Generic error for other path issues
                return false;
            }

            // CRUCIAL SECURITY CHECK: Ensure the resolved path is still within the intended root directory.
            // This comparison must be done on normalized, full paths.
            if (!resolvedPath.StartsWith(normalizedLoraRoot, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Access denied: LoRA identifier resolves outside the allowed directory.";
                Logs.Error($"[FileAccessHelper] Security: Path traversal attempt or misconfiguration. Identifier: '{relativeIdentifier}', Resolved: '{resolvedPath}', Root: '{normalizedLoraRoot}'");
                resolvedPath = null; // Clear path if security check fails
                return false;
            }

            try
            {
                if (!File.Exists(resolvedPath))
                {
                    errorMessage = $"LoRA file not found at '{normalizedRelativeIdentifier}'.";
                    Logs.Warning($"[FileAccessHelper] File not found for identifier '{relativeIdentifier}'. Looked at: '{resolvedPath}'");
                    resolvedPath = null; // Clear path as file doesn't exist
                    return false;
                }
            }
            catch (Exception ex) // Should not happen for File.Exists usually, but good for robustness
            {
                Logs.Error($"[FileAccessHelper] Error checking file existence for '{resolvedPath}': {ex.ToString()}");
                errorMessage = "Error verifying LoRA file existence.";
                resolvedPath = null;
                return false;
            }

            Logs.Info($"[FileAccessHelper] Successfully resolved identifier '{relativeIdentifier}' to path '{resolvedPath}'");
            return true;
        }
    }
}
