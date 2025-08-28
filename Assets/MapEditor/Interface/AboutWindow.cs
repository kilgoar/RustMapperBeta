using UnityEngine;
using UnityEngine.UI;

public class AboutWindow : MonoBehaviour
{
    [SerializeField] private Button patreonButton; // Assign in Inspector
    [SerializeField] private Button discordButton; // Assign in Inspector
	[SerializeField] private Button patreonButton1; // Assign in Inspector
    [SerializeField] private Button discordButton1; // Assign in Inspector
	[SerializeField] private Button hideBeg; // Assign in Inspector
	[SerializeField] private GameObject begPanel; // Assign in Inspector
	

    // Replace these with your actual links
    private string patreonUrl = "https://www.patreon.com/kilgoar";
    private string discordUrl = "https://discord.com/invite/PUHAafD5dw";

    void Start()
    {
		hideBeg.onClick.AddListener(OnHideBeg);
		
        if (patreonButton != null)
        {
            patreonButton.onClick.AddListener(OpenPatreon);
			patreonButton1.onClick.AddListener(OpenPatreon);
        }
        else
        {
            Debug.LogError("Patreon Button not assigned!");
        }

        if (discordButton != null)
        {
            discordButton.onClick.AddListener(OpenDiscord);
			discordButton1.onClick.AddListener(OpenDiscord);
        }
        else
        {
            Debug.LogError("Discord Button not assigned!");
        }
    }
	
	public void OnHideBeg(){
		begPanel.active = false;
	}

    public void OpenPatreon()
    {
        Application.OpenURL(patreonUrl);
    }

    public void OpenDiscord()
    {
        Application.OpenURL(discordUrl);
    }
}