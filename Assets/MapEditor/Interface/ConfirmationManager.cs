using System;
using System.Collections;
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
    private readonly Queue<(string title, string message, string yes, string no, TaskCompletionSource<bool> tcs)> dialogQueue 
        = new Queue<(string, string, string, string, TaskCompletionSource<bool>)>();
    private bool isProcessingQueue = false;

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

        var tcs = new TaskCompletionSource<bool>();
        dialogQueue.Enqueue((title, message, yes, no, tcs));

        if (!isProcessingQueue)
        {
            StartCoroutine(ProcessDialogQueue());
        }

        return await tcs.Task;
    }

    private IEnumerator ProcessDialogQueue()
    {
        isProcessingQueue = true;

        while (dialogQueue.Count > 0)
        {
            var dialog = dialogQueue.Dequeue();

            gameObject.transform.SetAsLastSibling();

            // Check for cached confirmation with matching title
            if (confirmationCache.TryGetValue(dialog.title, out var cachedConfirmation))
            {
                currentConfirmation = cachedConfirmation;
                currentConfirmation.gameObject.SetActive(true);
            }
            else
            {
                // Instantiate new confirmation if no match
                currentConfirmation = Instantiate(templateObject, gameObject.transform);
                currentConfirmation.gameObject.SetActive(true);
                confirmationCache[dialog.title] = currentConfirmation; // Add to cache
                ModManager.SkinGameObject(currentConfirmation.gameObject);
            }

            // Setup UI elements
            if (currentConfirmation.title != null)
                currentConfirmation.title.text = dialog.title;
            if (currentConfirmation.footer != null)
                currentConfirmation.footer.text = dialog.message;
            if (currentConfirmation.yes != null && currentConfirmation.yes.GetComponentInChildren<Text>() != null)
                currentConfirmation.yes.GetComponentInChildren<Text>().text = dialog.yes;
            if (currentConfirmation.no != null && currentConfirmation.no.GetComponentInChildren<Text>() != null)
                currentConfirmation.no.GetComponentInChildren<Text>().text = dialog.no;

            // Setup button listeners
            if (currentConfirmation.yes != null)
                currentConfirmation.yes.onClick.RemoveAllListeners(); // Clear previous listeners
            if (currentConfirmation.no != null)
                currentConfirmation.no.onClick.RemoveAllListeners();
            
            if (currentConfirmation.yes != null)
                currentConfirmation.yes.onClick.AddListener(() => OnButtonClicked(true, dialog.tcs));
            if (currentConfirmation.no != null)
                currentConfirmation.no.onClick.AddListener(() => OnButtonClicked(false, dialog.tcs));

            // Wait until the dialog is resolved (OnButtonClicked clears currentConfirmation)
            while (currentConfirmation != null)
            {
                yield return null;
            }
        }

        isProcessingQueue = false;
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

    public bool IsDialogActive() => currentConfirmation != null || isProcessingQueue;
}