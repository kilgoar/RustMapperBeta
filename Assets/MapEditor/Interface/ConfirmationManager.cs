using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class ConfirmationManager : MonoBehaviour
{
    public ConfirmationTemplate templateObject;
    
    public static ConfirmationManager Instance { get; private set; }
    
    private ConfirmationTemplate currentConfirmation;
    private readonly Dictionary<string, ConfirmationTemplate> confirmationCache = new Dictionary<string, ConfirmationTemplate>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        templateObject = Resources.Load<ConfirmationTemplate>("ConfirmationTemplate");
        if (templateObject == null)
        {
            Debug.LogError("Failed to load ConfirmationTemplate from Resources.");
        }
    }

    // Public method to show confirmation dialog and await response
    public async Task<bool> ShowConfirmationAsync(string title, string message, string yes = "Yes", string no = "No")
    {
        if (templateObject == null)
        {
            Debug.LogError("confirmation template not set.");
            return false;
        }

        if (currentConfirmation != null)
        {
            Debug.LogWarning("A confirmation dialog is already active.");
            return false;
        }
		
		gameObject.transform.SetAsLastSibling();

        var tcs = new TaskCompletionSource<bool>();

        // Check for cached confirmation with matching title
        if (confirmationCache.TryGetValue(title, out var cachedConfirmation))
        {
            currentConfirmation = cachedConfirmation;
            currentConfirmation.gameObject.SetActive(true);
        }
        else
        {
            // Instantiate new confirmation if no match
            currentConfirmation = Instantiate(templateObject, gameObject.transform);
			currentConfirmation.gameObject.SetActive(true);
            confirmationCache[title] = currentConfirmation; // Add to cache
			ModManager.SkinGameObject(currentConfirmation.gameObject);
        }

        // Setup UI elements
        if (currentConfirmation.title != null)
            currentConfirmation.title.text = title;
        if (currentConfirmation.footer != null)
            currentConfirmation.footer.text = message;
        if (currentConfirmation.yes != null && currentConfirmation.yes.GetComponentInChildren<Text>() != null)
            currentConfirmation.yes.GetComponentInChildren<Text>().text = yes;
        if (currentConfirmation.no != null && currentConfirmation.no.GetComponentInChildren<Text>() != null)
            currentConfirmation.no.GetComponentInChildren<Text>().text = no;

        // Setup button listeners
        if (currentConfirmation.yes != null)
            currentConfirmation.yes.onClick.RemoveAllListeners(); // Clear previous listeners
        if (currentConfirmation.no != null)
            currentConfirmation.no.onClick.RemoveAllListeners();
        
        if (currentConfirmation.yes != null)
            currentConfirmation.yes.onClick.AddListener(() => OnButtonClicked(true, tcs));
        if (currentConfirmation.no != null)
            currentConfirmation.no.onClick.AddListener(() => OnButtonClicked(false, tcs));

        return await tcs.Task;
    }

    private void OnButtonClicked(bool result, TaskCompletionSource<bool> tcs)
    {
        // Deactivate instead of destroying
        if (currentConfirmation != null)
        {
            currentConfirmation.gameObject.SetActive(false);
            currentConfirmation = null; // Clear current, keep in cache for reuse
        }

        // Set result
        tcs.SetResult(result);
    }
}