using UnityEngine;

[CreateAssetMenu(fileName = "DiscordConfig", menuName = "Discord/DiscordConfig", order = 1)]
public class DiscordConfigSO : ScriptableObject
{
    [SerializeField]
    private string clientId; // Store as string in Editor for ease of input
    [SerializeField]
    private string webBase = "https://discord.com/api"; // Default to correct API endpoint

    // Return client_id as string for Client constructor
    public string GetClientIdAsString()
    {
        Debug.Log($"Client ID (string): {clientId}, Web Base: {webBase}");
        if (string.IsNullOrEmpty(clientId))
        {
            Debug.LogError("Client ID is empty in DiscordConfigSO!");
        }
        return clientId;
    }

    // Convert string to ulong for SetApplicationId
    public ulong GetClientIdAsUlong()
    {
        if (ulong.TryParse(clientId, out ulong result))
        {
            Debug.Log($"Client ID (ulong): {result}");
            return result;
        }
        Debug.LogError("Invalid Client ID in DiscordConfigSO. Ensure it's a valid numeric string.");
        return 0;
    }

    public string GetWebBase()
    {
        return webBase;
    }
}