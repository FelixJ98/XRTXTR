using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.Threading.Tasks;

public class Auto1111 : MonoBehaviour
{
    [Header("Settings")]
    public string serverIP = "LocalHost";
    public float strength = 0.55f;
    public int steps = 30;
    public int cfgScale = 7;
    public string prompt = "dark steel texture, black metal, gunmetal finish";
    
    [Header("Textures")]
    public Texture2D input;
    public Texture2D mask;
    public GameObject target;
    
    string url => $"http://{serverIP}:7860/sdapi/v1/img2img";
    
    [ContextMenu("Process")]
    public async void ProcessTexture()
    {
        if (!input) { Debug.LogError("No input texture!"); return; }
        
        var result = mask ? await Inpaint() : await StyleTransfer();
        if (result) ApplyResult(result);
    }
    
    public async Task ProcessWithVoicePrompt(string voicePrompt)
    {
        if (!input) { Debug.LogError("No input texture!"); return; }
        
        string originalPrompt = prompt;
        prompt = voicePrompt;
        
        Debug.Log($"Starting AI processing with prompt: {voicePrompt}");
        
        try
        {
            var result = mask ? await Inpaint() : await StyleTransfer();
            if (result) 
            {
                ApplyResult(result);
                Debug.Log("AI completed!");
            }
            else
            {
                Debug.LogError("AI processing failed - no result returned");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"AI processing failed: {e.Message}");
        }
        finally
        {
            prompt = originalPrompt;
        }
    }
    
    async Task<Texture2D> StyleTransfer()
    {
        var data = new StyleTransferRequest();
        data.init_images = new[] { ToBase64(input) };
        data.prompt = $"{prompt}, preserve details, high quality";
        data.negative_prompt = "blurry, low quality, distorted";
        data.denoising_strength = strength;
        data.cfg_scale = cfgScale;
        data.steps = steps;
        data.width = input.width;
        data.height = input.height;
        
        return await SendRequest(data);
    }
    
    async Task<Texture2D> Inpaint()
    {
        var data = new InpaintRequest();
        data.init_images = new[] { ToBase64(input) };
        data.mask = ToBase64(mask);
        data.prompt = $"{prompt}, seamless, match lighting";
        data.negative_prompt = "visible seams, harsh edges";
        data.denoising_strength = strength;
        data.cfg_scale = cfgScale;
        data.steps = steps;
        data.width = input.width;
        data.height = input.height;
        data.inpaint_full_res = true;
        
        return await SendRequest(data);
    }
    
    async Task<Texture2D> SendRequest(object data)
    {
        try
        {
            Debug.Log("Sending request to AI server...");
            
            string json = JsonUtility.ToJson(data);
            
            using (var www = UnityWebRequest.PostWwwForm(url, ""))
            {
                www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                
                var operation = www.SendWebRequest();
                
                while (!operation.isDone)
                {
                    await Task.Yield();
                }
                
                if (www.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("AI server response received!");
                    var response = JsonUtility.FromJson<Response>(www.downloadHandler.text);
                    return FromBase64(response.images[0]);
                }
                else
                {
                    Debug.LogError($"Server request failed: {www.error}");
                    Debug.LogError($"Response code: {www.responseCode}");
                    if (!string.IsNullOrEmpty(www.downloadHandler.text))
                    {
                        Debug.LogError($"Server response: {www.downloadHandler.text}");
                    }
                }
            }
        }
        catch (Exception e) 
        { 
            Debug.LogError($"Network error: {e.Message}");
        }
        
        return null;
    }
    
    void ApplyResult(Texture2D tex)
    {
        if (target?.GetComponent<Renderer>() is Renderer r)
        {
            var newMaterial = new Material(r.material);
            newMaterial.mainTexture = tex;
            r.material = newMaterial;
            
            Debug.Log($"Applied new texture to {target.name}");
        }
        else
        {
            Debug.LogWarning("No target renderer found to apply texture");
        }
        
        SaveTexture(tex);
    }
    
    void SaveTexture(Texture2D tex)
    {
        try
        {
            var path = $"Assets/AI_result_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            
            #if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
            Debug.Log($"Saved result texture: {path}");
            #endif
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save texture: {e.Message}");
        }
    }
    
    public Texture2D CreateMask(float radius = 0.3f)
    {
        if (input == null)
        {
            Debug.LogError("Cannot create mask - no input texture assigned");
            return null;
        }
        
        var tex = new Texture2D(input.width, input.height);
        var pixels = new Color[input.width * input.height];
        var center = new Vector2(input.width * 0.5f, input.height * 0.5f);
        var maxDist = radius * Mathf.Min(input.width, input.height);
        
        for (int i = 0; i < pixels.Length; i++)
        {
            int x = i % input.width;
            int y = i / input.width;
            float dist = Vector2.Distance(new Vector2(x, y), center);
            float val = Mathf.SmoothStep(1f, 0f, dist / maxDist);
            pixels[i] = new Color(val, val, val, 1f);
        }
        
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
    
    string ToBase64(Texture2D tex)
    {
        if (tex == null)
        {
            Debug.LogError("Cannot convert null texture to base64");
            return "";
        }
        
        try
        {
            return Convert.ToBase64String(tex.EncodeToPNG());
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to encode texture to base64: {e.Message}");
            return "";
        }
    }
    
    Texture2D FromBase64(string base64)
    {
        if (string.IsNullOrEmpty(base64))
        {
            Debug.LogError("Cannot create texture from empty base64 string");
            return null;
        }
        
        try
        {
            var tex = new Texture2D(2, 2);
            tex.LoadImage(Convert.FromBase64String(base64));
            return tex;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to decode base64 to texture: {e.Message}");
            return null;
        }
    }
    
    // Proper classes instead of anonymous objects
    [Serializable]
    public class StyleTransferRequest
    {
        public string[] init_images;
        public string prompt;
        public string negative_prompt;
        public float denoising_strength;
        public int cfg_scale;
        public int steps;
        public int width;
        public int height;
    }
    
    [Serializable]
    public class InpaintRequest
    {
        public string[] init_images;
        public string mask;
        public string prompt;
        public string negative_prompt;
        public float denoising_strength;
        public int cfg_scale;
        public int steps;
        public int width;
        public int height;
        public bool inpaint_full_res;
    }
    
    [Serializable] 
    public class Response 
    { 
        public string[] images; 
    }
}