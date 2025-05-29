using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class Auto1111 : MonoBehaviour
{
    [Header("Server Settings - SAFE FOR GIT")]
    public string serverIP = "localhost";
    
    [Header("Smart Inpainting Settings")]
    public float denoisingStrength = 0.55f;
    public int cfgScale = 9;
    public int steps = 30;
    public int maskBlur = 12;
    
    [Header("Custom Prompt")]
    [TextArea(3, 6)]
    public string customPrompt = "dark steel texture, black metal, gunmetal finish";
    
    [Header("Manual Texture Assignment")]
    public Texture2D inputTexture; // Drag your PNG here
    public Texture2D maskTexture;  // Drag your mask PNG here (optional)
    public GameObject targetObject; // GameObject to apply result to (optional)
    
    private string ServerUrl => $"http://{serverIP}:7860";
    
    [System.Serializable]
    public class InpaintRequest
    {
        public string[] init_images;
        public string mask;  // Changed from string[] to string
        public string prompt;
        public string negative_prompt;
        public float denoising_strength;
        public int cfg_scale;
        public int steps;
        public string sampler_name = "DPM++ 2M";
        public int width = 512;
        public int height = 512;
        public int mask_blur_x = 4;  // Added - API expects separate X/Y blur
        public int mask_blur_y = 4;  // Added - API expects separate X/Y blur
        public int mask_blur = 0;    // Keep this but set to 0
        public bool inpaint_full_res = true;
        public int inpaint_full_res_padding = 32;
        public int inpainting_mask_invert = 0;
        public int inpainting_fill = 1;
        public int n_iter = 1;
        public int batch_size = 1;
        public int resize_mode = 0;  // Added - required field
        public bool send_images = true;  // Added - to get images back
        public bool save_images = false; // Added - don't save on server
    }
    
    [System.Serializable]
    public class APIResponse
    {
        public string[] images;
    }
    
    /// <summary>
    /// Process manually assigned textures from inspector - NOW USES NO MASK BY DEFAULT
    /// </summary>
    [ContextMenu("Process Manual Textures")]
    public async void ProcessManualTextures()
    {
        if (inputTexture == null)
        {
            Debug.LogError("No input texture assigned in inspector!");
            return;
        }
        
        // Check if texture is readable
        try
        {
            inputTexture.GetPixels();
        }
        catch (UnityException e)
        {
            Debug.LogError($"Input texture is not readable! Error: {e.Message}");
            Debug.LogError("Fix: Select texture → Inspector → Read/Write Enabled ✓ → Format: RGBA 32 bit → Apply");
            return;
        }
        
        Debug.Log($"Processing texture with NO MASK (style transfer): {inputTexture.name} ({inputTexture.width}x{inputTexture.height})");
        Debug.Log($"Texture format: {inputTexture.format}");
        Debug.Log($"Texture size in memory: {inputTexture.width * inputTexture.height * 4} bytes");
        
        // Use style transfer (no mask) by default
        string stylePrompt = $"{customPrompt}, maintain car parts structure, preserve vehicle details, realistic automotive texture, high detail";
        string styleNegative = "solid color, flat texture, loss of detail, destroyed geometry, unrecognizable, simple texture, blurry";
        
        Texture2D result = await DoImg2Img(inputTexture, stylePrompt, styleNegative);
        
        if (result != null)
        {
            Debug.Log("Texture transformation completed!");
            
            // Optionally apply to target object
            if (targetObject != null)
            {
                ApplyTextureToGameObject(targetObject, result);
            }
            
            // Save result to file
            SaveTextureToFile(result, "transformed_texture");
        }
        else
        {
            Debug.LogError("Texture transformation failed!");
        }
    }
    
    /// <summary>
    /// Smart inpainting using your custom prompt with context awareness
    /// </summary>
    public async System.Threading.Tasks.Task<Texture2D> TransformWithCustomPrompt(
        Texture2D inputTexture,
        Texture2D maskTexture)
    {
        string smartPrompt = $"{customPrompt}, seamless integration, match surrounding lighting, preserve detail level, smooth blending";
        string smartNegative = "visible seams, harsh edges, lighting mismatch, low quality, blurry";
        
        return await DoInpaint(inputTexture, maskTexture, smartPrompt, smartNegative);
    }
    
    /// <summary>
    /// Smart inpainting with manual prompt override
    /// </summary>
    public async System.Threading.Tasks.Task<Texture2D> TransformWithPrompt(
        Texture2D inputTexture,
        Texture2D maskTexture,
        string manualPrompt)
    {
        string smartPrompt = $"{manualPrompt}, seamless integration, match surrounding lighting, preserve detail level, smooth blending";
        string smartNegative = "visible seams, harsh edges, lighting mismatch, low quality, blurry";
        
        return await DoInpaint(inputTexture, maskTexture, smartPrompt, smartNegative);
    }
    
    /// <summary>
    /// Create a smooth circular mask for better blending
    /// </summary>
    public Texture2D CreateSmoothMask(int width, int height, float centerX = 0.5f, float centerY = 0.5f, float radius = 0.3f)
    {
        Texture2D mask = new Texture2D(width, height);
        Color[] pixels = new Color[width * height];
        Vector2 center = new Vector2(centerX * width, centerY * height);
        float maxRadius = radius * Mathf.Min(width, height);
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float maskValue = Mathf.Clamp01(1f - (distance / maxRadius));
                maskValue = Mathf.SmoothStep(0f, 1f, maskValue); // Smooth edges
                
                pixels[y * width + x] = new Color(maskValue, maskValue, maskValue, 1f);
            }
        }
        
        mask.SetPixels(pixels);
        mask.Apply();
        return mask;
    }
    
    /// <summary>
    /// Create a gradient mask that's stronger in center, weaker at edges
    /// This preserves edge details while allowing center transformation
    /// </summary>
    public Texture2D CreateGradientMask(int width, int height)
    {
        Texture2D mask = new Texture2D(width, height);
        Color[] pixels = new Color[width * height];
        
        Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
        float maxDistance = Vector2.Distance(Vector2.zero, center);
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                // Create gradient from center (white) to edges (gray)
                float maskValue = Mathf.Lerp(0.8f, 0.2f, distance / maxDistance);
                
                pixels[y * width + x] = new Color(maskValue, maskValue, maskValue, 1f);
            }
        }
        
        mask.SetPixels(pixels);
        mask.Apply();
        return mask;
    }
    
    /// <summary>
    /// Process with gradient mask for better detail preservation
    /// </summary>
    [ContextMenu("Process with Gradient Mask")]
    public async void ProcessWithGradientMask()
    {
        if (inputTexture == null)
        {
            Debug.LogError("No input texture assigned!");
            return;
        }
        
        Debug.Log("Processing with gradient mask (preserves edges)...");
        
        Texture2D gradientMask = CreateGradientMask(inputTexture.width, inputTexture.height);
        Texture2D result = await Transform(inputTexture, gradientMask);
        
        if (result != null)
        {
            Debug.Log("Gradient mask processing completed!");
            
            if (targetObject != null)
            {
                ApplyTextureToGameObject(targetObject, result);
            }
            
            SaveTextureToFile(result, "gradient_mask_result");
        }
    }
    
    /// <summary>
    /// Apply the transformed texture to a GameObject
    /// </summary>
    public void ApplyTextureToGameObject(GameObject obj, Texture2D newTexture)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            // Create new material instance to avoid affecting other objects
            Material newMaterial = new Material(renderer.material);
            newMaterial.mainTexture = newTexture;
            renderer.material = newMaterial;
            Debug.Log($"Applied texture to {obj.name} renderer");
            return;
        }
        
        UnityEngine.UI.Image uiImage = obj.GetComponent<UnityEngine.UI.Image>();
        if (uiImage != null)
        {
            Sprite newSprite = Sprite.Create(newTexture, 
                new Rect(0, 0, newTexture.width, newTexture.height), 
                new Vector2(0.5f, 0.5f));
            uiImage.sprite = newSprite;
            Debug.Log($"Applied texture to {obj.name} UI Image");
            return;
        }
        
        Debug.LogWarning($"Could not apply texture to {obj.name} - no Renderer or UI Image component found");
    }
    
    /// <summary>
    /// Save transformed texture to Assets folder
    /// </summary>
    public void SaveTextureToFile(Texture2D texture, string fileName)
    {
        byte[] pngData = texture.EncodeToPNG();
        string path = $"Assets/{fileName}_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
        System.IO.File.WriteAllBytes(path, pngData);
        
        #if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
        Debug.Log($"Saved transformed texture to: {path}");
        #endif
    }
    
    private async System.Threading.Tasks.Task<Texture2D> DoInpaint(
        Texture2D inputTexture,
        Texture2D maskTexture,
        string prompt,
        string negativePrompt)
    {
        try
        {
            Debug.Log($"Connecting to: {ServerUrl}/sdapi/v1/img2img");
            Debug.Log($"Prompt: {prompt}");
            
            var request = new InpaintRequest
            {
                init_images = new[] { TextureToBase64(inputTexture) },
                mask = TextureToBase64(maskTexture),  // Single string, not array
                prompt = prompt,
                negative_prompt = negativePrompt,
                denoising_strength = denoisingStrength,
                cfg_scale = cfgScale,
                steps = steps,
                mask_blur_x = maskBlur,  // Use your maskBlur setting for both X and Y
                mask_blur_y = maskBlur,
                width = inputTexture.width,   // Use actual texture dimensions
                height = inputTexture.height
            };
            
            string jsonData = JsonUtility.ToJson(request);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            
            using (UnityWebRequest www = new UnityWebRequest($"{ServerUrl}/sdapi/v1/img2img", "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                
                Debug.Log("Sending request to server...");
                var operation = www.SendWebRequest();
                while (!operation.isDone)
                {
                    await System.Threading.Tasks.Task.Yield();
                }
                
                Debug.Log($"Response Code: {www.responseCode}");
                
                if (www.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("Server response received successfully!");
                    var response = JsonUtility.FromJson<APIResponse>(www.downloadHandler.text);
                    return Base64ToTexture(response.images[0]);
                }
                else
                {
                    Debug.LogError($"Inpainting failed: {www.error}");
                    Debug.LogError($"Full Response: {www.downloadHandler.text}");
                    Debug.LogError($"Request JSON: {jsonData}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Inpainting error: {e.Message}");
        }
        
        return null;
    }
    
    private string TextureToBase64(Texture2D texture)
    {
        return Convert.ToBase64String(texture.EncodeToPNG());
    }
    
    private Texture2D Base64ToTexture(string base64)
    {
        byte[] imageBytes = Convert.FromBase64String(base64);
        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(imageBytes);
        return texture;
    }
    
    // Main transformation method - uses inspector prompt
    public async System.Threading.Tasks.Task<Texture2D> Transform(Texture2D input, Texture2D mask) =>
        await TransformWithCustomPrompt(input, mask);
    
    /// <summary>
    /// Automatically process textures when the game starts - NOW USES NO MASK
    /// </summary>
    private async void Start()
    {
        // Wait a moment for everything to initialize
        await System.Threading.Tasks.Task.Delay(1000);
        
        // Check if we have an input texture assigned
        if (inputTexture != null)
        {
            Debug.Log("Auto-processing texture on start with NO MASK...");
            ProcessManualTextures(); // Now uses no mask by default
        }
        else
        {
            Debug.LogWarning("No input texture assigned - skipping auto-process on start");
        }
    }
    
    /// <summary>
    /// Style transfer - changes material/style while preserving car details
    /// Uses img2img without mask for better detail preservation
    /// </summary>
    [ContextMenu("Style Transfer (No Mask)")]
    public async void StyleTransfer()
    {
        if (inputTexture == null)
        {
            Debug.LogError("No input texture assigned!");
            return;
        }
        
        Debug.Log("Starting style transfer (preserving car details)...");
        
        // Use moderate denoising strength for style transfer
        float originalStrength = denoisingStrength;
        denoisingStrength = 0.65f; // Higher than before but still preserves structure
        
        string stylePrompt = $"{customPrompt}, maintain car parts structure, preserve vehicle details, realistic automotive texture, high detail";
        string styleNegative = "solid color, flat texture, loss of detail, destroyed geometry, unrecognizable, simple texture, blurry";
        
        Texture2D result = await DoImg2Img(inputTexture, stylePrompt, styleNegative);
        
        // Restore original setting
        denoisingStrength = originalStrength;
        
        if (result != null)
        {
            Debug.Log("Style transfer completed!");
            
            if (targetObject != null)
            {
                ApplyTextureToGameObject(targetObject, result);
            }
            
            SaveTextureToFile(result, "style_transfer");
        }
    }
    
    /// <summary>
    /// img2img without mask - better for style transfer
    /// </summary>
    private async System.Threading.Tasks.Task<Texture2D> DoImg2Img(
        Texture2D inputTexture,
        string prompt,
        string negativePrompt)
    {
        try
        {
            Debug.Log($"Style transfer with denoising: {denoisingStrength}");
            
            var request = new Img2ImgRequest
            {
                init_images = new[] { TextureToBase64(inputTexture) },
                prompt = prompt,
                negative_prompt = negativePrompt,
                denoising_strength = denoisingStrength,
                cfg_scale = cfgScale,
                steps = steps,
                width = inputTexture.width,
                height = inputTexture.height
            };
            
            string jsonData = JsonUtility.ToJson(request);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            
            using (UnityWebRequest www = new UnityWebRequest($"{ServerUrl}/sdapi/v1/img2img", "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                
                var operation = www.SendWebRequest();
                while (!operation.isDone)
                {
                    await System.Threading.Tasks.Task.Yield();
                }
                
                if (www.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<APIResponse>(www.downloadHandler.text);
                    return Base64ToTexture(response.images[0]);
                }
                else
                {
                    Debug.LogError($"Style transfer failed: {www.error}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Style transfer error: {e.Message}");
        }
        
        return null;
    }
    
    [System.Serializable]
    public class Img2ImgRequest
    {
        public string[] init_images;
        public string prompt;
        public string negative_prompt;
        public float denoising_strength;
        public int cfg_scale;
        public int steps;
        public string sampler_name = "DPM++ 2M";
        public int width = 512;
        public int height = 512;
        public int n_iter = 1;
        public int batch_size = 1;
        public bool send_images = true;
        public bool save_images = false;
    }
}