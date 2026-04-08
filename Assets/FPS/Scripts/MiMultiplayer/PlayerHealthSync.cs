using Unity.Netcode;
using UnityEngine;
using Unity.FPS.Game;

public class PlayerHealthSync : NetworkBehaviour
{
    Health m_Health;

    void Awake()
    {
        // Cogemos el script de vida original
        m_Health = GetComponent<Health>();
    }

    // Esta función es llamada por Damageable.cs usando SendMessage
    // Nota: usamos object para poder mandar (damage, damageSource) sin acoplar por overloads.
    void OnNetworkDamageRequested(object payload)
    {
        float damage = 0f;
        GameObject damageSource = null;

        if (payload is float f)
        {
            damage = f;
        }
        else if (payload is object[] arr && arr.Length >= 1)
        {
            if (arr[0] is float f0) damage = f0;
            if (arr.Length >= 2) damageSource = arr[1] as GameObject;
        }

        // El ordenador que ha disparado la bala le pide al servidor que aplique el daño
        if (IsServer)
        {
            ApplyDamageClientRpc(damage, damageSource);
        }
        else
        {
            RequestDamageServerRpc(damage, damageSource);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void RequestDamageServerRpc(float damage, NetworkObjectReference damageSourceRef)
    {
        ApplyDamageClientRpc(damage, damageSourceRef);
    }

    // El servidor da la orden y esto se ejecuta en las pantallas de TODOS los jugadores
    [ClientRpc]
    void ApplyDamageClientRpc(float damage, NetworkObjectReference damageSourceRef)
    {
        if (m_Health != null)
        {
            // Ahora sí, ejecutamos el daño real en el script original de cada ordenador a la vez
            GameObject damageSource = null;
            if (damageSourceRef.TryGet(out NetworkObject no) && no != null)
                damageSource = no.gameObject;

            m_Health.TakeDamage(damage, damageSource);
        }
    }

    NetworkObjectReference ToNetRefOrNull(GameObject go)
    {
        if (go == null) return default;
        var netObj = go.GetComponent<NetworkObject>();
        if (netObj == null) return default;
        return netObj;
    }

    void ApplyDamageClientRpc(float damage, GameObject damageSource)
    {
        ApplyDamageClientRpc(damage, ToNetRefOrNull(damageSource));
    }

    void RequestDamageServerRpc(float damage, GameObject damageSource)
    {
        RequestDamageServerRpc(damage, ToNetRefOrNull(damageSource));
    }
}