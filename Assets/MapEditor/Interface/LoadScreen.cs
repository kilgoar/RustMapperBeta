using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LoadScreen : MonoBehaviour
{
    public RectTransform transform;
    public Image progress, progress1, frame;
    public Text loadMessage, loadMessage1;
    public bool isEnabled;
	
	public bool completed = false;
	public bool completed1 = false;
	public Button patreonButton, discordButton;
    
    public static LoadScreen Instance { get; private set; }
    
    private string patreonUrl = "https://www.patreon.com/kilgoar";
    private string discordUrl = "https://discord.com/invite/PUHAafD5dw";

    public void OpenPatreon()
    {
        Application.OpenURL(patreonUrl);
    }

    public void OpenDiscord()
    {
        Application.OpenURL(discordUrl);
    }
	
	
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
    
	public void Progress(float percent)
	{
		if (progress == null || frame == null) return; // Safety check

		// Clamp percent between 0 and 1
		percent = Mathf.Clamp01(percent);

		RectTransform frameRect = frame.rectTransform;
		RectTransform progressRect = progress.rectTransform;

		float frameWidth = frameRect.rect.width;
		float newWidth = frameWidth * 0.95f * percent;

		// This method respects anchors
		progressRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, newWidth);
	}
	
	public void Complete(int f){
		if (f==0){ completed = true; }
		else if(f==1){ completed1 = true; }
		
		if (completed && completed1){
			completed = false;
			completed1 = false;
			Hide();
		}
	}
	
	public void Progress1(float percent)
	{
		if (progress == null || frame == null) return; // Safety check

		// Clamp percent between 0 and 1
		percent = Mathf.Clamp01(percent);

		RectTransform frameRect = frame.rectTransform;
		RectTransform progressRect = progress1.rectTransform;

		float frameWidth = frameRect.rect.width;
		float newWidth = frameWidth * 0.95f * percent;

		// This method respects anchors
		progressRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, newWidth);
	}
    
    public void SetMessage(string message)    
    {
        if (loadMessage != null)        
        {
            loadMessage.text = message;
        }
    }
	
	public void SetMessage1(string message)    
    {
        if (loadMessage != null)        
        {
            loadMessage1.text = message;
        }
    }
    
	public void Show()    {
		completed = false;
		completed1 = false;

		CameraManager.Instance.cam.cullingMask = 0;
		Application.targetFrameRate = -1;
        StartCoroutine(RotateLoadingScreen());
		Application.runInBackground = true;		
		
		var canvas = gameObject.GetComponent<Canvas>();
		if (canvas != null)
		{
			canvas.enabled = true;
		}	
		
        Progress(0);
		Progress1(0);
        SetMessage("Loading...");
		SetMessage1("Loading...");
        isEnabled = true;
		
		MenuManager.Instance.Hide();
		Compass.Instance.Hide();
    }
    
	public void Hide()	{
		
		var canvas = gameObject.GetComponent<Canvas>();
		
		if (canvas != null)		{
			canvas.enabled = false;
		}
		
		isEnabled = false;

		CameraManager.Instance.cam.cullingMask = -1;
        StopAllCoroutines();

		MenuManager.Instance.Show();
		Compass.Instance.Show();
	}
	
	public void Start(){
		Show();
		
		patreonButton.onClick.AddListener(OpenPatreon);
		discordButton.onClick.AddListener(OpenDiscord);
	}
	
	
    
    IEnumerator RotateLoadingScreen()
    {
        while (true)        
        {
            transform.Rotate(0, 0, -20 * Time.deltaTime);
            yield return null;
        }
    }
}