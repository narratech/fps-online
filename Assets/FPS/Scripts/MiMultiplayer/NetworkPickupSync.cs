using Unity.Netcode;
using UnityEngine;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;

public class NetworkPickupSync : NetworkBehaviour
{
    private WeaponPickup m_WeaponPickup;
    private bool isPickedUp = false; 

    void Awake()
    {
        m_WeaponPickup = GetComponent<WeaponPickup>();
    }

    // Esta función la llama mágicamente el SendMessage del WeaponPickup
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
                        GetComponent<LocalWorldPickupRespawn>()?.TryScheduleRespawnAtCurrentTransform();
                }
            }
        }
    }
}