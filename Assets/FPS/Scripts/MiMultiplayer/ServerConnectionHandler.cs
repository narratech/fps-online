using System.Text;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Connection Approval (NGO): el servidor decide si aprueba la conexión y qué prefab de jugador usar.
/// <para>
/// Este proyecto usa `ConnectionData` como payload: `"nickname_index"`.
/// </para>
/// <para>
/// Responsabilidades:
/// - Parsear el payload recibido.
/// - Seleccionar el prefab de player en función de un índice hacia <see cref="UniversidadesList"/>.
/// - Notificar a <see cref="MatchDataManager"/> del nuevo jugador (solo si existe en escena).
/// </para>
/// <para>
/// DEFECTUOSO (validación): el payload no se valida con rigor (encoding, caracteres, longitud, índice, etc.).
/// Un cliente podría enviar valores inesperados. Para producción conviene:
/// - validar longitud y charset,
/// - clamp del índice,
/// - sanitizar el nombre,
/// - usar `rpcParams.Receive.SenderClientId`/request.ClientNetworkId como fuente de verdad del id.
/// </para>
/// <para>
/// Nota de proyecto (reglas NGO): los prefabs seleccionables deben estar registrados SOLO en `UniversidadesPrefabsList.asset`
/// para evitar *duplicate GlobalObjectIdHash* y problemas de hash de NetworkConfig.
/// </para>
/// </summary>
public class ServerConnectionHandler : MonoBehaviour
{
    [Tooltip("Arrastra aquí tu archivo UniversidadesPrefabsList.")]
    /// <summary>Lista de prefabs de player seleccionables (registrada en NGO).</summary>
    public NetworkPrefabsList UniversidadesList;

    /// <summary>Se registra al callback de approval en el `NetworkManager`.</summary>
    void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
        }
    }

    /// <summary>
    /// Callback de NGO para aprobar una conexión y seleccionar el prefab de jugador.
    /// </summary>
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

        // Log de approval desactivado para no ensuciar consola en runtime.

        // El portero avisa al Cerebro del nuevo jugador
        if (MatchDataManager.Instance != null)
        {
            MatchDataManager.Instance.RegisterPlayer(request.ClientNetworkId, playerName);
        }
    }
}