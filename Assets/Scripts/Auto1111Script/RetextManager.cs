using UnityEngine;
using Oculus.Interaction.HandGrab;
using TMPro;

public class RetextManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI promptText;
    public string currentPrompt = "dark steel texture, black metal, gunmetal finish";
    
    [Header("Meta XR Setup")]
    public HandGrabInteractor leftHandGrabber;
    public HandGrabInteractor rightHandGrabber;
    
    [Header("Retexture Settings")]
    public string serverIP = "localhost";
    public float strength = 0.55f;
    public int steps = 30;
    
    private Auto1111 retextureScript;
    private GameObject currentGrabbedObject;
    
    void Start()
    {
        // Create the retexture script dynamically
        retextureScript = gameObject.AddComponent<Auto1111>();
        retextureScript.serverIP = serverIP;
        retextureScript.strength = strength;
        retextureScript.steps = steps;
        
        // Update UI
        UpdatePromptDisplay();
    }
    
    void Update()
    {
        // Check what object is currently being grabbed
        currentGrabbedObject = GetCurrentGrabbedObject();
    }
    
    GameObject GetCurrentGrabbedObject()
    {
        // Check both hands for grabbed objects
        if (leftHandGrabber != null && leftHandGrabber.SelectedInteractable != null)
            return leftHandGrabber.SelectedInteractable.transform.gameObject;
        
        if (rightHandGrabber != null && rightHandGrabber.SelectedInteractable != null)
            return rightHandGrabber.SelectedInteractable.transform.gameObject;
        
        return null;
    }
    
    // Call this from your UI button
    public async void ProcessCurrentObject()
    {
        if (currentGrabbedObject == null)
        {
            Debug.LogWarning("No object grabbed! Grab an object first.");
            return;
        }
        
        // Get the renderer from grabbed object
        Renderer renderer = currentGrabbedObject.GetComponent<Renderer>();
        if (renderer == null)
        {
            Debug.LogError("Grabbed object has no Renderer component!");
            return;
        }
        
        // Get current texture - check TextureProvider first, then fallback to material
        Texture2D currentTexture = null;
        
        TextureProvider textureProvider = currentGrabbedObject.GetComponent<TextureProvider>();
        if (textureProvider != null && textureProvider.GetTexture() != null)
        {
            currentTexture = textureProvider.GetTexture();
            Debug.Log($"Using TextureProvider texture: {currentTexture.name}");
        }
        else
        {
            currentTexture = renderer.material.mainTexture as Texture2D;
            Debug.Log("Using material texture");
        }
        
        if (currentTexture == null)
        {
            Debug.LogError("No valid texture found!");
            return;
        }
        
        // Setup the retexture script with current object data
        retextureScript.input = currentTexture;
        retextureScript.target = currentGrabbedObject;
        retextureScript.prompt = currentPrompt;
        
        string mode = retextureScript.mask ? "inpainting" : "style transfer";
        Debug.Log($"Retexturing {currentGrabbedObject.name} using {mode} with prompt: {currentPrompt}");
        
        // Process the texture
        await retextureScript.ProcessWithVoicePrompt(currentPrompt);
    }
    
    // Call this when dictation updates the prompt
    public void UpdatePrompt(string newPrompt)
    {
        currentPrompt = newPrompt;
        UpdatePromptDisplay();
        Debug.Log($"Prompt updated to: {currentPrompt}");
    }
    
    void UpdatePromptDisplay()
    {
        if (promptText != null)
        {
            promptText.text = $"Prompt: {currentPrompt}";
        }
    }
    
    // Get current grabbed object name for UI display
    public string GetCurrentObjectName()
    {
        return currentGrabbedObject ? currentGrabbedObject.name : "None";
    }
}
