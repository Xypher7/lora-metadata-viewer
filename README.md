# LoRA Metadata Viewer/Editor

Single file pure HTML tool for viewing and editing LoRA metadata locally on your web browser without the need of installation or internet connectivity.

## Usage
No need for prior setup, just open the HTML file with the web browser of your choice and drag a Safetensors file. Alternatively, you can use the version hosted on GitHub through the link provided in the demo section.

## Demo
https://xypher7.github.io/lora-metadata-viewer

## Features
- Configurable metadata summary
- CivitAI resource lookup. (Requires internet connection)
- Configurable training tag summary
- Edit or remove metadata in the Safetensors file
- Doro :3

## Offline Execution
The tool is designed to be used offline and processing is entirely done on your browser. However, the following features require fetching some resources on the internet and will not work without internet connectivity:
- CivitAI data lookup
- Processing large files (greater than 2GB)
    - Optionally, if you wish to maintain everything local, you may download the <a name="unique-anchor-name" href='https://cdn.jsdelivr.net/npm/hash-wasm@4/dist/sha256.umd.min.js'>sha256.umd.min.js</a> file, place it in the same directory as the HTML file, and replace this line
        ```html
        <script src="https://cdn.jsdelivr.net/npm/hash-wasm@4/dist/sha256.umd.min.js"></script>
        ```
        with 
        ```html
        <script src="sha256.umd.min.js"></script>
        ```

## Screenshots
![App Screenshot](https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/ed823c08-4551-40b4-badd-aab544d463dc/original=true,quality=90/Screenshot%202024-09-05%20235613.jpeg)
