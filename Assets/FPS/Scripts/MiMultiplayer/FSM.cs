using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Unity.FPS.Gameplay;
using Unity.FPS.Game;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Controlador automático (bot) para un jugador.
/// Pensado para ponerse en el prefab `UCM_Bot` en lugar de `ClientPlayerMove`.
/// </summary>
public class FSM : NetworkBehaviour
{
    [Header("Autopilot")]
    [SerializeField] float wanderRadius = 25f;
    [SerializeField] float repathIntervalSeconds = 1.25f;
    [SerializeField] float stoppingDistance = 1.5f;

    [Header("Respawn")]
    [SerializeField] float respawnDelaySeconds = 4f;
    [SerializeField] float minSpawnDistanceFromPlayers = 4f;

    NavMeshAgent m_Agent;
    float m_NextRepathTime;
    Health m_Health;
    Coroutine m_ServerRespawnRoutine;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Solo mueve el bot en la instancia que lo posee (normalmente el host/servidor en vuestro setup).
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        // Importante: si el prefab todavía tiene el script de jugador humano, éste puede reactivar controles y CC
        // y acabar empujando/arrastrando a otros jugadores.
        var humanController = GetComponent<NewMonoBehaviourScript>();
        if (humanController != null) humanController.enabled = false;

        var respawner = GetComponent<Unity.FPS.Gameplay.PlayerRespawner>();
        if (respawner != null) respawner.enabled = false;

        m_Health = GetComponent<Health>();
        if (m_Health != null)
        {
            m_Health.OnDie += OnDied;
            m_Health.OnHealed += OnHealed;
        }

        DisableManualControlComponents();
        EnsureAgent();
    }

    public override void OnNetworkDespawn()
    {
        if (m_Health != null)
        {
            m_Health.OnDie -= OnDied;
            m_Health.OnHealed -= OnHealed;
        }

        base.OnNetworkDespawn();
    }

    void DisableManualControlComponents()
    {
        var inputHandler = GetComponent<PlayerInputHandler>();
        if (inputHandler != null) inputHandler.enabled = false;

        var weapons = GetComponent<PlayerWeaponsManager>();
        if (weapons != null) weapons.enabled = false;

        var pcc = GetComponent<PlayerCharacterController>();
        if (pcc != null) pcc.enabled = false;

        var jetpack = GetComponent<Jetpack>();
        if (jetpack != null) jetpack.enabled = false;

        var playerInput = GetComponent<PlayerInput>();
        if (playerInput != null) playerInput.enabled = false;

        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
    }

    void EnsureAgent()
    {
        if (m_Agent == null)
            m_Agent = GetComponent<NavMeshAgent>();
        if (m_Agent == null)
            m_Agent = gameObject.AddComponent<NavMeshAgent>();

        m_Agent.enabled = true;
        m_Agent.stoppingDistance = Mathf.Max(0.25f, stoppingDistance);
        m_Agent.autoBraking = true;
        m_Agent.updatePosition = true;
        m_Agent.updateRotation = true;
    }

    void OnDied()
    {
        // Nunca mover un cadáver (evita que "se arrastre" tras morir)
        StopAgent();

        // Respawn automático del bot (servidor)
        if (IsServer)
        {
            if (m_ServerRespawnRoutine != null)
                StopCoroutine(m_ServerRespawnRoutine);
            m_ServerRespawnRoutine = StartCoroutine(ServerRespawnRoutine());
        }
    }

    void OnHealed(float _)
    {
        // Si revive, podemos volver a moverlo (el servidor sincronizará posición vía NetworkTransform)
        ResumeAgent();
        m_NextRepathTime = 0f;
    }

    void StopAgent()
    {
        if (m_Agent == null) return;
        if (m_Agent.enabled)
        {
            m_Agent.isStopped = true;
            m_Agent.ResetPath();
        }
        m_Agent.enabled = false;
    }

    void ResumeAgent()
    {
        EnsureAgent();
        if (m_Agent != null && m_Agent.enabled)
            m_Agent.isStopped = false;
    }

    IEnumerator ServerRespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelaySeconds);

        // Elegimos punto de respawn evitando jugadores ya presentes.
        var spawnPoints = GameObject.FindGameObjectsWithTag("RespawnPoint");
        PickSpawnPointAvoidingPlayers(spawnPoints, minSpawnDistanceFromPlayers, out var spawnPos, out var spawnRot);

        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.SetPositionAndRotation(spawnPos, spawnRot);
        if (cc != null) cc.enabled = true;

        if (m_Health != null)
            m_Health.Revive();

        m_ServerRespawnRoutine = null;
    }

    void Update()
    {
        if (!IsOwner) return;
        if (m_Health != null && m_Health.CurrentHealth <= 0f) return;
        if (m_Agent == null || !m_Agent.enabled) return;
        if (!m_Agent.isOnNavMesh) return;

        if (Time.time < m_NextRepathTime) return;
        m_NextRepathTime = Time.time + Mathf.Max(0.1f, repathIntervalSeconds);

        bool needsNewTarget = !m_Agent.hasPath || m_Agent.pathPending ||
                              m_Agent.remainingDistance <= (m_Agent.stoppingDistance + 0.35f);
        if (!needsNewTarget) return;

        if (TryPickRandomNavMeshPoint(transform.position, Mathf.Max(2f, wanderRadius), out var dest))
            m_Agent.SetDestination(dest);
    }

    static void PickSpawnPointAvoidingPlayers(GameObject[] spawnPoints, float minDistance, out Vector3 spawnPos,
        out Quaternion spawnRot)
    {
        spawnPos = new Vector3(0, 5, 0);
        spawnRot = Quaternion.identity;

        if (spawnPoints == null || spawnPoints.Length == 0)
            return;

        var players = Object.FindObjectsByType<PlayerCharacterController>(FindObjectsSortMode.None);

        for (int attempt = 0; attempt < 24; attempt++)
        {
            int idx = Random.Range(0, spawnPoints.Length);
            var sp = spawnPoints[idx];
            if (sp == null) continue;

            Vector3 candidate = sp.transform.position;
            bool blocked = false;

            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null) continue;
                if (Vector3.Distance(players[i].transform.position, candidate) < minDistance)
                {
                    blocked = true;
                    break;
                }
            }

            if (!blocked)
            {
                spawnPos = candidate;
                spawnRot = sp.transform.rotation;
                return;
            }
        }

        var fallback = spawnPoints[Random.Range(0, spawnPoints.Length)];
        if (fallback != null)
        {
            spawnPos = fallback.transform.position;
            spawnRot = fallback.transform.rotation;
        }
    }

    static bool TryPickRandomNavMeshPoint(Vector3 origin, float radius, out Vector3 result)
    {
        for (int i = 0; i < 20; i++)
        {
            var rnd = Random.insideUnitSphere * radius;
            var candidate = origin + rnd;
            if (NavMesh.SamplePosition(candidate, out var hit, 2.0f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }

        result = origin;
        return false;
    }
}

