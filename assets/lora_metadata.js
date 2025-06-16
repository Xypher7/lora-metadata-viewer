document.addEventListener('DOMContentLoaded', () => {
    console.log("LoRA Metadata JS loaded for event-driven interaction.");

    const metadataOutput = document.getElementById('metadataOutput');
    const civitaiSection = document.getElementById('civitaiSection');
    const fetchCivitaiBtn = document.getElementById('fetchCivitaiBtn');
    const civitaiInfoOutput = document.getElementById('civitaiInfoOutput');
    const civitaiSeparator = document.querySelector('.civitai-separator');


    let currentLoraIdentifier = null;

    // Initial UI State
    if(metadataOutput) metadataOutput.innerHTML = '<p>Select a LoRA from the SwarmUI LoRA browser to view its metadata here.</p>';
    if(civitaiInfoOutput) civitaiInfoOutput.innerHTML = ''; // Clear initially
    if(fetchCivitaiBtn) fetchCivitaiBtn.style.display = 'none'; // Should be controlled by civitaiSection visibility
    if(civitaiSection) civitaiSection.style.display = 'none';
    if(civitaiSeparator) civitaiSeparator.style.display = 'none';


    // Check if essential elements exist
    if (!metadataOutput || !civitaiSection || !fetchCivitaiBtn || !civitaiInfoOutput || !civitaiSeparator) {
        console.error('One or more essential UI elements are missing from lora_metadata_tab.html. Aborting script.');
        if(metadataOutput) metadataOutput.innerHTML = '<p class="error">Critical UI elements missing. Extension cannot function.</p>';
        return;
    }

    async function handleLoraSelection(event) {
        console.log("swarmUILoRaSelected event received:", event.detail);
        // Clear previous state
        currentLoraIdentifier = null;
        metadataOutput.innerHTML = '';
        civitaiInfoOutput.innerHTML = '';
        fetchCivitaiBtn.style.display = 'none';
        civitaiSection.style.display = 'none';
        civitaiSeparator.style.display = 'none';


        const loraDetail = event.detail;

        if (!loraDetail || !loraDetail.loraIdentifier) {
            metadataOutput.innerHTML = '<p class="error">Error: Invalid LoRA selection event data received.</p>';
            return;
        }

        currentLoraIdentifier = loraDetail.loraIdentifier;
        const loraName = loraDetail.loraName || currentLoraIdentifier.split(/[\\/]/).pop().replace(/\.(safetensors|ckpt|pt)/i, '');


        metadataOutput.innerHTML = `<p>Loading metadata for: <strong>${escapeHtml(loraName)}</strong> (<code>${escapeHtml(currentLoraIdentifier)}</code>)...</p>`;

        try {
            const response = await fetch('/API/LoraMetadata', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ loraIdentifier: currentLoraIdentifier })
            });

            const data = await response.json(); // Try to parse JSON regardless of response.ok for error messages

            if (!response.ok) {
                throw new Error(data?.error || `HTTP error! Status: ${response.status}`);
            }

            if (data.error) { // Handle application-level errors from backend
                 throw new Error(data.error);
            }

            if (data.localMetadata && Object.keys(data.localMetadata).length > 0) {
                let htmlContent = `<h3>Metadata for ${escapeHtml(data.loraName || loraName)}</h3>`;
                // Assuming colorizeJSON returns an HTML string that needs to be appended.
                // If colorizeJSON returns a DOM element, use appendChild.
                htmlContent += colorizeJSON(data.localMetadata);
                metadataOutput.innerHTML = htmlContent;
            } else {
                metadataOutput.innerHTML = `<p>No local metadata found for <strong>${escapeHtml(data.loraName || loraName)}</strong>.</p>`;
            }

            // Show CivitAI section and button
            civitaiSeparator.style.display = 'block';
            civitaiSection.style.display = 'block';
            fetchCivitaiBtn.style.display = 'inline-block';
            civitaiInfoOutput.innerHTML = '<p>Ready to fetch CivitAI information for the selected LoRA.</p>';

        } catch (error) {
            console.error('Error fetching local LoRA metadata:', error);
            metadataOutput.innerHTML = `<p class="error">Error loading local metadata for <strong>${escapeHtml(loraName)}</strong>: ${escapeHtml(error.message)}</p>`;
            currentLoraIdentifier = null; // Reset on error
            civitaiSeparator.style.display = 'none';
            civitaiSection.style.display = 'none';
        }
    }

    document.addEventListener('swarmUILoRaSelected', handleLoraSelection);

    fetchCivitaiBtn.addEventListener('click', async () => {
        if (!currentLoraIdentifier) {
            civitaiInfoOutput.innerHTML = '<p class="error" style="color: orange;">No LoRA selected or initial data failed to load. Please select a LoRA from the browser again.</p>';
            return;
        }

        const loraNameForDisplay = currentLoraIdentifier.split(/[\\/]/).pop().replace(/\.(safetensors|ckpt|pt)/i, '');
        civitaiInfoOutput.innerHTML = `<p>Fetching CivitAI information for <strong>${escapeHtml(loraNameForDisplay)}</strong>...</p>`;

        try {
            const response = await fetch('/API/LoraCivitaiLookup', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ loraIdentifier: currentLoraIdentifier })
            });

            const civitaiResult = await response.json(); // Try to parse JSON regardless of response.ok

            if (!response.ok) {
                throw new Error(civitaiResult?.error || `HTTP error! Status: ${response.status}`);
            }

            if (civitaiResult.error) { // Handle application-level errors
                throw new Error(civitaiResult.error);
            }

            if (civitaiResult.found && civitaiResult.data) {
                let html = `<h3>Found on CivitAI!</h3>`;
                html += `<p><strong>Hash Used:</strong> ${escapeHtml(civitaiResult.hashUsed || 'N/A')} (<code>${escapeHtml(civitaiResult.hashValue || 'N/A')}</code>)</p>`;

                const modelVersion = civitaiResult.data;
                if (modelVersion.model) {
                     html += `<p><strong>Model Name:</strong> ${escapeHtml(modelVersion.model.name || 'N/A')}</p>`;
                     if (modelVersion.model.creator) {
                        html += `<p><strong>Creator:</strong> <a href="https://civitai.com/user/${escapeHtml(modelVersion.model.creator.username)}" target="_blank">${escapeHtml(modelVersion.model.creator.username || 'N/A')}</a></p>`;
                     }
                }
                html += `<p><strong>Version Name:</strong> ${escapeHtml(modelVersion.name || 'N/A')}</p>`;
                if (modelVersion.trainedWords && modelVersion.trainedWords.length > 0) {
                    html += `<p><strong>Trained Words:</strong> ${modelVersion.trainedWords.map(tw => `<code>${escapeHtml(tw)}</code>`).join(', ')}</p>`;
                }
                if (modelVersion.id) { // Link to the model version page
                    html += `<p><a href="https://civitai.com/models/${modelVersion.modelId}?modelVersionId=${modelVersion.id}" target="_blank">View on Civitai</a></p>`;
                }

                if (modelVersion.images && modelVersion.images.length > 0) {
                    const previewImage = modelVersion.images.find(img => img.type === 'image'); // Prefer 'image' type
                    if (previewImage && previewImage.url) {
                        // Resize URL for preview, if CivitAI supports width/height query params for images (common pattern)
                        const previewUrl = previewImage.url.replace(/\/width=\d+/, '') + (previewImage.url.includes('?') ? '&' : '?') + 'width=300';
                        html += `<h4>Preview Image:</h4>`;
                        html += `<a href="https://civitai.com/models/${modelVersion.modelId}?modelVersionId=${modelVersion.id}" target="_blank">`;
                        html += `<img src="${escapeHtml(previewUrl)}" alt="CivitAI Preview" style="max-width: 300px; max-height: 450px; border: 1px solid #ccc; margin-top: 5px; object-fit: contain;">`;
                        html += `</a>`;
                    }
                }
                html += `<div style="margin-top:15px;"><h4>Full CivitAI Data:</h4></div>`;
                // Assuming colorizeJSON returns an HTML string
                html += colorizeJSON(civitaiResult.data);
                civitaiInfoOutput.innerHTML = html;
            } else {
                civitaiInfoOutput.innerHTML = '<p style="color: orange;">Could not find this LoRA on CivitAI using AutoV2 or AutoV3 hashes.</p>';
            }
        } catch (error) {
            console.error('Error fetching CivitAI info:', error);
            civitaiInfoOutput.innerHTML = `<p class="error">Failed to fetch CivitAI info: ${escapeHtml(error.message)}</p>`;
        }
    });

    // Ensure escapeHtml and colorizeJSON are defined (as per previous steps)
    function escapeHtml(unsafe) {
        if (unsafe === null || typeof unsafe === 'undefined') return '';
        return unsafe
             .toString()
             .replace(/&/g, "&amp;")
             .replace(/</g, "&lt;")
             .replace(/>/g, "&gt;")
             .replace(/"/g, "&quot;")
             .replace(/'/g, "&#039;");
    }

    function colorizeJSON(jsonObj) {
        if (jsonObj === null || typeof jsonObj !== 'object') {
            // For non-objects, just stringify and escape, wrapped in pre
            return `<pre class="json-container">${escapeHtml(JSON.stringify(jsonObj, null, 2))}</pre>`;
        }

        const jsonString = JSON.stringify(jsonObj, null, 2);
        // Basic escape for the whole string, then apply spans.
        // This prevents HTML injection if values themselves contain < or >.
        let html = escapeHtml(jsonString);

        // Regex for keys, strings, numbers, booleans, null
        // Note: This regex is simpler and applies to the escaped string.
        // It won't create clickable URLs directly from here unless URLs are post-processed or handled differently.
        html = html.replace(/("(\\u[a-zA-Z0-9]{4}|\\[^u]|[^\\"])*"(\s*:)?)/g, (match) => {
            let cls = 'json-value-string';
            if (/:$/.test(match)) {
                cls = 'json-key';
            }
            // For URLs in strings, this would require unescaping, checking, then re-escaping parts.
            // Simpler: The colorizeJSON function handles structure, not complex data types like embedded URLs.
            // URLs would be colored as strings. If specific URL highlighting is needed, it's more complex.
            return `<span class="${cls}">${match}</span>`;
        });
        html = html.replace(/\b(true|false)\b/g, '<span class="json-value-boolean">$1</span>');
        html = html.replace(/\b(null)\b/g, '<span class="json-value-null">$1</span>');
        // Improved number regex to avoid matching parts of strings or other numbers like in UUIDs if they were not quoted.
        html = html.replace(/(?<![a-zA-Z0-9])(-?\d+(\.\d+)?([eE][+-]?\d+)?)(?![a-zA-Z0-9])/g, '<span class="json-value-number">$1</span>');

        return `<pre class="json-container">${html}</pre>`;
    }
});
