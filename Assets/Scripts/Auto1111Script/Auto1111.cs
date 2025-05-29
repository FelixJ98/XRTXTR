using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class Auto1111 : MonoBehaviour
{
    [Header("Settings")]
    public string serverIP = "localhost";
    public float strength = 0.55f;
    public int steps = 30;
    public string prompt = "dark steel texture, black metal, gunmetal finish";
    
    [Header("Textures")]
    public Texture2D input;
    public Texture2D mask;
    public GameObject target;
    
    string url => $"http://{serverIP}:7860/sdapi/v1/img2img";
    
    void Start() => ProcessTexture();
    
    [ContextMenu("Process")]
    public async void ProcessTexture()
    {
        if (!input) { Debug.LogError("No input texture!"); return; }
        
        var result = mask ? await Inpaint() : await StyleTransfer();
        if (result) ApplyResult(result);
    }
    
    // Takes voice prompt
    public async System.Threading.Tasks.Task ProcessWithVoicePrompt(string voicePrompt)
    {
        if (!input) { Debug.LogError("No input texture!"); return; }
        
        string originalPrompt = prompt;
        prompt = voicePrompt;
        
        Debug.Log($"Processing with prompt: {voicePrompt}");
        
        var result = mask ? await Inpaint() : await StyleTransfer();
        if (result) ApplyResult(result);
        
        prompt = originalPrompt;
    }
    
    async System.Threading.Tasks.Task<Texture2D> StyleTransfer()
    {
        var data = new {
            init_images = new[] { ToBase64(input) },
            prompt = $"{prompt}, preserve details, high quality",
            negative_prompt = "blurry, low quality, distorted",
            denoising_strength = strength,
            steps = steps,
            width = input.width,
            height = input.height
        };
        
        return await SendRequest(data);
    }
    
    async System.Threading.Tasks.Task<Texture2D> Inpaint()
    {
        var data = new {
            init_images = new[] { ToBase64(input) },
            mask = ToBase64(mask),
            prompt = $"{prompt}, seamless, match lighting",
            negative_prompt = "visible seams, harsh edges",
            denoising_strength = strength,
            steps = steps,
            width = input.width,
            height = input.height,
            inpaint_full_res = true
        };
        
        return await SendRequest(data);
    }
    
    async System.Threading.Tasks.Task<Texture2D> SendRequest(object data)
    {
        try
        {
            string json = JsonUtility.ToJson(data);
            var www = UnityWebRequest.PostWwwForm(url, "");
            www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            www.SetRequestHeader("Content-Type", "application/json");
            
            await www.SendWebRequest();
            
            if (www.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<Response>(www.downloadHandler.text);
                return FromBase64(response.images[0]);
            }
            
            Debug.LogError($"Failed: {www.error}");
        }
        catch (Exception e) { Debug.LogError(e.Message); }
        
        return null;
    }
    
    void ApplyResult(Texture2D tex)
    {
        if (target?.GetComponent<Renderer>() is Renderer r)
        {
            r.material = new Material(r.material) { mainTexture = tex };
        }
        
        SaveTexture(tex);
        Debug.Log("Done!");
    }
    
    void SaveTexture(Texture2D tex)
    {
        var path = $"Assets/result_{DateTime.Now:HHmmss}.png";
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        
        #if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
        #endif
    }
    
    public Texture2D CreateMask(float radius = 0.3f)
    {
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
    
    string ToBase64(Texture2D tex) => Convert.ToBase64String(tex.EncodeToPNG());
    
    Texture2D FromBase64(string base64)
    {
        var tex = new Texture2D(2, 2);
        tex.LoadImage(Convert.FromBase64String(base64));
        return tex;
    }
    
    [Serializable] class Response { public string[] images; }
}