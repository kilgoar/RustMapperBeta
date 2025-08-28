using UnityEngine;
using Discord.Sdk;

public class DiscordManager : MonoBehaviour
{
    [SerializeField]
    private DiscordConfigSO discordConfig; // Assign in Inspector

    private Client client;

    void Start()
    {
        if (discordConfig == null)
        {
            Debug.LogError("DiscordConfig is not assigned in the Inspector!");
            return;
        }

        // Initialize Discord client
        try
        {
            client = new Client(discordConfig.GetClientIdAsString(), discordConfig.GetWebBase());
            client.AddLogCallback(OnLog, LoggingSeverity.Verbose); // Verbose for detailed logs
            client.SetStatusChangedCallback(OnStatusChanged);

            // Set application ID as ulong
            ulong appId = discordConfig.GetClientIdAsUlong();
            if (appId != 0)
            {
                client.SetApplicationId(appId);
            }
            else
            {
                Debug.LogError("Cannot set application ID: Invalid client ID");
                return;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to initialize Discord client: {e.Message}");
            return;
        }

        // Set Rich Presence without connecting
        UpdatePresence();
    }

    private void OnLog(string message, LoggingSeverity severity)
    {
        Debug.Log($"Discord SDK Log: {severity} - {message}");
    }

    private void OnStatusChanged(Client.Status status, Client.Error error, int errorCode)
    {
        Debug.Log($"Client status changed: {status}, Error: {error}, ErrorCode: {errorCode}");
        if (error != Client.Error.None)
        {
            Debug.LogError($"Client error: {error}, code: {errorCode}");
        }
    }

    private void UpdatePresence()
    {
        Activity activity = new Activity();
        activity.SetType(ActivityTypes.Playing);
        activity.SetState("RustMapper");
        activity.SetDetails("https://rustmapper.com");
        client.UpdateRichPresence(activity, (ClientResult result) =>
        {
            if (result.Successful())
            {
                Debug.Log("Rich Presence updated successfully");
            }
            else
            {
                Debug.LogError($"Failed to update Rich Presence: {result.Error()}");
            }
        });
    }

    void OnDestroy()
    {
        client?.Dispose();
    }
}