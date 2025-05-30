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
    public string serverIP = "LocalHost";
    public float strength = 0.55f;
    public int steps = 30;
    public int cfgScale = 13;

    private Auto1111 retextureScript;
    private GameObject currentGrabbedObject;
    
    void Start()
    {
        // Create the retexture script dynamically
        retextureScript = gameObject.AddComponent<Auto1111>();
        retextureScript.serverIP = serverIP;
        retextureScript.strength = strength;
        retextureScript.steps = steps;
        retextureScript.cfgScale = cfgScale;
        
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
    
    // Search for the first MeshRenderer starting from the grabbed object
    MeshRenderer meshRenderer = null;
    GameObject targetObject = null;
    
    // First check the grabbed object itself
    meshRenderer = currentGrabbedObject.GetComponent<MeshRenderer>();
    if (meshRenderer != null)
    {
        targetObject = currentGrabbedObject;
    }
    
    // If not found, search in children
    if (meshRenderer == null)
    {
        meshRenderer = currentGrabbedObject.GetComponentInChildren<MeshRenderer>();
        if (meshRenderer != null)
        {
            targetObject = meshRenderer.gameObject;
        }
    }
    
    // If still not found, search up the hierarchy
    if (meshRenderer == null)
    {
        Transform current = currentGrabbedObject.transform.parent;
        while (current != null && meshRenderer == null)
        {
            meshRenderer = current.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                targetObject = current.gameObject;
                break;
            }
            current = current.parent;
        }
    }
    
    if (meshRenderer == null)
    {
        Debug.LogError("No MeshRenderer found!");
        return;
    }
    
    // Get current texture - check TextureProvider first, then fallback to material
    Texture2D currentTexture = null;
    
    TextureProvider textureProvider = targetObject.GetComponent<TextureProvider>();
    if (textureProvider != null && textureProvider.GetTexture() != null)
    {
        currentTexture = textureProvider.GetTexture();
    }
    else
    {
        currentTexture = meshRenderer.material.mainTexture as Texture2D;
    }
    
    if (currentTexture == null)
    {
        Debug.LogError("No valid texture found!");
        return;
    }
    
    // Setup the retexture script
    retextureScript.input = currentTexture;
    retextureScript.target = targetObject;
    retextureScript.prompt = currentPrompt;
    
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
        return currentGrabbedObject ? currentGrabbedObject.transform.parent.name : "None";
    }
}
