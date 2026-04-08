using System.Text;
using Unity.Netcode;
using UnityEngine;

public class ServerConnectionHandler : MonoBehaviour
{
    [Tooltip("Arrastra aquí tu archivo UniversidadesPrefabsList.")]
    public NetworkPrefabsList UniversidadesList;

    void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
        }
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        string payloadString = Encoding.ASCII.GetString(request.Payload);
        string[] payloadParts = payloadString.Split('_');

        string playerName = "JugadorDesconocido";
        int characterIndex = 0;

        if (payloadParts.Length >= 2)
        {
            playerName = payloadParts[0];
            int.TryParse(payloadParts[1], out characterIndex);
        }

        response.Approved = true;
        response.CreatePlayerObject = true;

        if (UniversidadesList != null && characterIndex >= 0 && characterIndex < UniversidadesList.PrefabList.Count)
        {
            GameObject selectedPrefab = UniversidadesList.PrefabList[characterIndex].Prefab;

            if (selectedPrefab != null)
            {
                var netObj = selectedPrefab.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    response.PlayerPrefabHash = netObj.PrefabIdHash;
                }
                else
                {
                    Debug.LogError($"[Approval] Prefab '{selectedPrefab.name}' no tiene NetworkObject.");
                }
            }
        }
        else
        {
            Debug.LogError($"[Approval] characterIndex fuera de rango ({characterIndex}). ListCount={(UniversidadesList != null ? UniversidadesList.PrefabList.Count : -1)} Payload='{payloadString}'");
        }

        Debug.Log($"[Approval] clientId={request.ClientNetworkId} payload='{payloadString}' prefabHash={response.PlayerPrefabHash} approved={response.Approved}");

        // El portero avisa al Cerebro del nuevo jugador
        if (MatchDataManager.Instance != null)
        {
            MatchDataManager.Instance.RegisterPlayer(request.ClientNetworkId, playerName);
        }
    }
}