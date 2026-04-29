using Unity.Netcode;
using UnityEngine;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;

/// <summary>
/// Sincroniza la recogida de un `WeaponPickup` (arma en el mundo) entre clientes usando NGO.
/// <para>
/// Idea: la colisión/interacción se detecta localmente (por el `WeaponPickup` del sample) y se pide al servidor
/// que conceda el arma a un cliente concreto; el servidor arbitra la carrera y despawnea el pickup.
/// </para>
/// <para>
/// <b>DEFECTUOSO (validación):</b> el servidor confía en el `clientId` que llega en el ServerRpc.
/// Sin comprobaciones adicionales, un cliente malicioso podría solicitar pickups para otro id o sin estar
/// realmente dentro del trigger. En un entorno no confiable, el servidor debería validar:
/// - que el RPC lo envía ese `clientId` (ver `rpcParams.Receive.SenderClientId`),
/// - que el jugador está cerca del pickup,
/// - que el pickup sigue disponible.
/// </para>
/// </summary>
public class NetworkPickupSync : NetworkBehaviour
{
    /// <summary>
    /// Referencia al componente original `WeaponPickup` del FPS Sample que realmente concede el arma.
    /// </summary>
    private WeaponPickup m_WeaponPickup;
    /// <summary>
    /// Flag autoritativo simple para evitar dobles concesiones (lo fija el servidor).
    /// </summary>
    private bool isPickedUp = false; 

    void Awake()
    {
        m_WeaponPickup = GetComponent<WeaponPickup>();
    }

    // Esta función la llama mágicamente el SendMessage del WeaponPickup
    /// <summary>
    /// Callback invocado por `WeaponPickup` (vía <c>SendMessage</c>) cuando alguien intenta recoger el objeto.
    /// </summary>
    /// <param name="byPlayer">Controlador del jugador que realizó la interacción local.</param>
    void OnNetworkPickupRequested(PlayerCharacterController byPlayer)
    {
        // Nos aseguramos de que el que tocó el arma es TÚ muñeco, no el clon de tu amigo en tu pantalla
        NetworkObject playerNetObj = byPlayer.GetComponentInParent<NetworkObject>();
        if (playerNetObj != null && playerNetObj.IsOwner)
        {
            // Petición al Servidor
            RequestPickupServerRpc(playerNetObj.OwnerClientId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    /// <summary>
    /// RPC cliente→servidor solicitando recoger el pickup para un cliente.
    /// </summary>
    void RequestPickupServerRpc(ulong clientId)
    {
        // Si el servidor ve que otro jugador se te adelantó, rechaza tu petición
        if (isPickedUp) return;

        isPickedUp = true; // El servidor bloquea el arma

        // El servidor manda el arma
        GrantWeaponClientRpc(clientId);

        Invoke(nameof(DespawnWeapon), 0.1f);
    }

    void DespawnWeapon()
    {
        // Despawn en servidor. Se evita destruir el GameObject si el pickup estaba colocado en escena.
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            // Si el objeto es "in-scene placed", destruirlo puede provocar warnings.
            // Preferimos despawnear sin destruir; si quieres que desaparezca visualmente, desactívalo.
            netObj.Despawn(false);
            gameObject.SetActive(false);
        }
    }

    [ClientRpc]
    /// <summary>
    /// RPC servidor→clientes notificando quién ganó la carrera; solo el cliente ganador ejecuta la concesión real.
    /// </summary>
    void GrantWeaponClientRpc(ulong clientId)
    {
        // Solo el ordenador del que ganó la carrera ejecuta esto
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            if (m_WeaponPickup != null)
            {
                // Buscamos a tu jugador local y le obligamos a ejecutar la función original
                GameObject localPlayerObj = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject().gameObject;
                PlayerCharacterController localPlayer = localPlayerObj.GetComponent<PlayerCharacterController>();

                if (localPlayer != null)
                {
                    bool granted = m_WeaponPickup.GrantWeapon(localPlayer);
                    if (granted)
                        LocalWorldPickupRespawn.ScheduleLocalRespawnFor(gameObject);
                }
            }
        }
    }
}