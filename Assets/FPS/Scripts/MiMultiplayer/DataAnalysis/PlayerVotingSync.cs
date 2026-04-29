using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Sincronización de "votos" (clasificación Humano/Robot) hacia el servidor para análisis de partida.
/// <para>
/// Uso esperado: el jugador local pulsa un botón/UI apuntando a otro jugador, y este componente envía un
/// ServerRpc con (tiradorId, objetivoId, tipo de voto).
/// </para>
/// <para>
/// Autoridad:
/// - El owner decide la intención (<see cref="SubmitVoteHumano"/> / <see cref="SubmitVoteRobot"/>).
/// - El servidor registra el voto en <see cref="MatchDataManager"/> (si existe).
/// </para>
/// <para>
/// Nota: este sistema no intenta validar "si puedes votar" (cooldown, distancia, repetición, etc.).
/// Si se necesita integridad de datos, el servidor debería validar y deduplicar.
/// </para>
/// </summary>
public class PlayerVotingSync : NetworkBehaviour
{
    /// <summary>
    /// Envia un voto "Humano" sobre un objetivo.
    /// </summary>
    /// <param name="targetGameObject">GameObject del objetivo (o un hijo) que tenga `NetworkObject` en su jerarquía.</param>
    public void SubmitVoteHumano(GameObject targetGameObject)
    {
        ProcesarVoto(targetGameObject, true);
    }

    /// <summary>
    /// Envia un voto "Robot" sobre un objetivo.
    /// </summary>
    /// <param name="targetGameObject">GameObject del objetivo (o un hijo) que tenga `NetworkObject` en su jerarquía.</param>
    public void SubmitVoteRobot(GameObject targetGameObject)
    {
        ProcesarVoto(targetGameObject, false);
    }

    /// <summary>
    /// Valida mínimamente que el caller es owner y que el objetivo es un player object, y envía el voto al servidor.
    /// </summary>
    /// <param name="targetGameObject">Objeto objetivo.</param>
    /// <param name="esVotoHumano">Tipo de voto.</param>
    private void ProcesarVoto(GameObject targetGameObject, bool esVotoHumano)
    {
        if (!IsOwner) return;

        NetworkObject targetNetObj = targetGameObject.GetComponentInParent<NetworkObject>();

        if (targetNetObj != null && targetNetObj.IsPlayerObject)
        {
            SendVoteServerRpc(OwnerClientId, targetNetObj.OwnerClientId, esVotoHumano);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    /// <summary>
    /// RPC cliente→servidor para registrar el voto en el agregado de partida.
    /// </summary>
    /// <remarks>
    /// RequiereOwnership=false para permitir que el voto sea enviado incluso si el objeto que tiene este script
    /// no es estrictamente "propiedad" del cliente en ciertos setups. Si el objeto sí es del jugador, se podría
    /// endurecer.
    /// </remarks>
    void SendVoteServerRpc(ulong tiradorId, ulong objetivoId, bool esVotoHumano, ServerRpcParams rpcParams = default)
    {
        if (MatchDataManager.Instance != null)
        {
            MatchDataManager.Instance.RegistrarVoto(tiradorId, objetivoId, esVotoHumano);
        }
    }
}