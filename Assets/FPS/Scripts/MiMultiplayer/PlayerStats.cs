using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Unity.FPS.Game;

/// <summary>
/// Estadísticas por jugador (nombre, kills, muertes) sincronizadas para que todos puedan ver el marcador.
/// Autoridad: servidor.
/// </summary>
public class PlayerStats : NetworkBehaviour
{
    public NetworkVariable<FixedString64Bytes> PlayerName = new(
        "",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> Kills = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> Deaths = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    Health m_Health;
    GameObject m_LastDamageSource;

    void Awake()
    {
        m_Health = GetComponent<Health>();
        if (m_Health != null)
        {
            m_Health.OnDamaged += OnDamaged;
            m_Health.OnDie += OnDie;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            // Tomamos el nombre del PlayerNameTag si existe (se replica como owner-write),
            // y lo copiamos a una variable server-write para el marcador.
            var nameTag = GetComponent<PlayerNameTag>();
            if (nameTag != null)
            {
                PlayerName.Value = nameTag.NetworkedName.Value;
                nameTag.NetworkedName.OnValueChanged += OnNameChangedServer;
            }
            else
            {
                PlayerName.Value = new FixedString64Bytes($"Player {OwnerClientId}");
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            var nameTag = GetComponent<PlayerNameTag>();
            if (nameTag != null)
            {
                nameTag.NetworkedName.OnValueChanged -= OnNameChangedServer;
            }
        }

        base.OnNetworkDespawn();
    }

    void OnNameChangedServer(FixedString64Bytes prev, FixedString64Bytes next)
    {
        if (!IsServer) return;
        PlayerName.Value = next;
    }

    void OnDamaged(float damage, GameObject damageSource)
    {
        // Guardamos el último atacante para poder asignar kill si morimos.
        if (damageSource != null)
            m_LastDamageSource = damageSource;
    }

    void OnDie()
    {
        if (!IsServer) return;

        Deaths.Value++;

        var killerStats = FindKillerStats(m_LastDamageSource);
        if (killerStats != null && killerStats != this)
        {
            killerStats.Kills.Value++;
        }
    }

    public static PlayerStats FindKillerStats(GameObject damageSource)
    {
        if (damageSource == null) return null;

        // El damageSource suele ser el GameObject del jugador que disparó (Owner del proyectil).
        var stats = damageSource.GetComponentInParent<PlayerStats>();
        if (stats != null) return stats;

        // A veces puede venir un hijo (arma/brazo/cámara). Subimos por jerarquía.
        return damageSource.GetComponent<PlayerStats>();
    }
}

