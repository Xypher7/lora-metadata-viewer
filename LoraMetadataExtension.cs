using SwarmUI.Core;
using SwarmUI.WebAPI;
using System.Collections.Generic;

namespace SwarmUI.Extensions.LoRAViewer
{
    public class LoraMetadataExtension : Extension
    {
        public static string RootExtensionPath;

        public override void OnPreInit()
        {
            // It's common to set a root path for assets if SwarmUI doesn't handle it automatically
            // This path might need adjustment based on how SwarmUI structures extension files.
            // Assuming files are in "MyExtensionName/assets/" relative to the extension DLL.
            // For now, let's assume SwarmUI handles asset paths relative to a known extension asset URL.
            // RootExtensionPath = $"{Program.ServerSettings.Address}:{Program.ServerSettings.Port}/Extensions/LoraMetadataExtension";
        }

        public override void OnInit()
        {
            Logs.Init("[LoraMetadataExtension] Initializing LoRA Metadata Viewer Extension...");

            // Register the tab
            // The path to the HTML file needs to be resolvable by SwarmUI's web server.
            // This often involves a base path for the extension's assets.
            // Let's assume an "/Assets/ExtensionName/" pattern or similar that SwarmUI might use.
            // The URL path for assets might be different from the file system path.
            // For example, SwarmUI might map "extensions/LoraMetadataExtension/assets/file.js" to a physical path.
            API.RegisterTab(
                id: "lora_metadata",
                name: "LoRA Metadata",
                url: "/Assets/lora_metadata_tab.html", // This URL will be prefixed by SwarmUI's extension asset serving logic
                visible: () => true // Or based on some condition/user setting
            );

            // Register JavaScript and CSS files
            // These paths are also relative to how SwarmUI serves extension assets.
            ScriptFiles.Add("/Assets/lora_metadata.js");
            StyleSheetFiles.Add("/Assets/lora_metadata.css");

            // Register the API endpoint
            LoraMetadataAPI.Register();
            LoraCivitaiLookupAPI.Register(); // Register the new Civitai lookup API

            Logs.Info("[LoraMetadataExtension] LoRA Metadata Viewer Extension Initialized with all API endpoints.");
        }

        // If SwarmUI requires explicit asset registration beyond just ScriptFiles/StyleSheetFiles (e.g. for HTML)
        // It might look something like this, but this is speculative.
        // Built-in extensions would be the best reference.
        // For now, we assume RegisterTab handles the HTML, and ScriptFiles/StyleSheetFiles are sufficient.
        /*
        public override void OnAssetsRequested(List<WebServer.WebAsset> assets)
        {
            assets.Add(new WebServer.WebAsset()
            {
                Path = "/lora_metadata_tab.html",
                ContentType = "text/html",
                Data = () => WebServer.GetFileBytes("extensions/LoraMetadataExtension/assets/lora_metadata_tab.html") // Example path
            });
            assets.Add(new WebServer.WebAsset()
            {
                Path = "/lora_metadata.js",
                ContentType = "application/javascript",
                Data = () => WebServer.GetFileBytes("extensions/LoraMetadataExtension/assets/lora_metadata.js")
            });
            assets.Add(new WebServer.WebAsset()
            {
                Path = "/lora_metadata.css",
                ContentType = "text/css",
                Data = () => WebServer.GetFileBytes("extensions/LoraMetadataExtension/assets/lora_metadata.css")
            });
        }
        */
    }
}
