using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Unity.FPS.Gameplay;
using Unity.FPS.Game;

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

    NavMeshAgent m_Agent;
    float m_NextRepathTime;
    Health m_Health;

    void Awake()
    {
        // MUY importante en MPPM: esto corre antes de Start() de otros componentes.
        // El bot NO debe tocar input/cursor ni ejecutar lógica de armas basada en input.
        DisableInputAndWeaponSystems();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        m_Health = GetComponent<Health>();
        if (m_Health != null)
        {
            m_Health.OnDie += OnDied;
            m_Health.OnHealed += OnHealed;
        }

        // En instancias remotas (no-owner) nunca debemos tener cámara/listener activos (MPPM: rompe al host).
        if (!IsOwner)
        {
            DisableCameraAndAudioForNonOwner();
            return;
        }

        DisableHumanControllersThatFightNavMeshAgent();
        EnsureAgent();
    }

    void DisableCameraAndAudioForNonOwner()
    {
        foreach (var cam in GetComponentsInChildren<Camera>(true))
            cam.enabled = false;

        foreach (var listener in GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;

        // Si hay cámaras en hijos enteros, desactivarlas evita scripts colgando de esos GOs.
        foreach (var cam in GetComponentsInChildren<Camera>(true))
        {
            if (cam != null && cam.gameObject != null)
                cam.gameObject.SetActive(false);
        }
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

    void DisableInputAndWeaponSystems()
    {
        var inputHandler = GetComponent<PlayerInputHandler>();
        if (inputHandler != null) inputHandler.enabled = false;

        var weapons = GetComponent<PlayerWeaponsManager>();
        if (weapons != null) weapons.enabled = false;

        // No deshabilitamos PlayerRespawner ni Health: el bot debe morir/respawnear igual que los humanos.
    }

    void DisableHumanControllersThatFightNavMeshAgent()
    {
        // Si dejamos el stack de control humano activo, su Update puede sobrescribir el movimiento y el bot "no se mueve".
        var pcc = GetComponent<PlayerCharacterController>();
        if (pcc != null) pcc.enabled = false;

        var jetpack = GetComponent<Jetpack>();
        if (jetpack != null) jetpack.enabled = false;

        var playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
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

