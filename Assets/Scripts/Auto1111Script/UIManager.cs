using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Meta.WitAi.Dictation;
using UnityEngine.Serialization;

public class UIManager : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI promptDisplay;
    public TextMeshProUGUI objectDisplay;
    public Button retextureButton;
    public Button recordButton;
    
    [Header("Dictation")]
    public DictationService dictationService; // Drag your App Dictation Experience here
    
    [Header("Manager")]
    public RetextManager retextureManager;
    
    private bool isRecording = false;
    
    void Start()
    {
        // Setup button listeners
        retextureButton.onClick.AddListener(() => retextureManager.ProcessCurrentObject());
        recordButton.onClick.AddListener(ToggleRecording);
        
        // Setup dictation callbacks
        if (dictationService != null)
        {
            dictationService.DictationEvents.OnFullTranscription.AddListener(OnTranscription);
            dictationService.DictationEvents.OnStartListening.AddListener(() => isRecording = true);
            dictationService.DictationEvents.OnStoppedListening.AddListener(() => isRecording = false);
        }
    }
    
    void Update()
    {
        // Update UI displays
        if (objectDisplay != null)
        {
            string objName = retextureManager.GetCurrentObjectName();
            objectDisplay.text = $"Object: {objName}";
            
            // Enable/disable retexture button based on grabbed object
            retextureButton.interactable = (objName != "None");
        }
        
        // Update record button text
        if (recordButton != null)
        {
            recordButton.GetComponentInChildren<TextMeshProUGUI>().text = isRecording ? "Stop Recording" : "Record";
        }
    }
    
    void ToggleRecording()
    {
        if (!isRecording)
        {
            dictationService.Activate();
            Debug.Log("Started recording...");
        }
        else
        {
            dictationService.Deactivate();
            Debug.Log("Stopped recording...");
        }
    }
    
    void OnTranscription(string transcription)
    {
        retextureManager.UpdatePrompt(transcription);
        Debug.Log($"Voice prompt received: {transcription}");
    }
}
