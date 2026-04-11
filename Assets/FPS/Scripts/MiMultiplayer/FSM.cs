using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Unity.FPS.Gameplay;
using Unity.FPS.Game;

// =================================================================================================
// FSM — Máquina de estados MUY simplificada para UCM_Bot (plantilla para alumnos)
// =================================================================================================
// Objetivo:
//   • Separar "qué decide la IA" (esta clase) de "cómo se ejecutan las acciones en el juego"
//     (ver BotGameplayActions).
//   • Dejar el camino claro para que podáis sustituir el comportamiento de Wander por persecución,
//     cobertura, captura de flags, etc.
//
// Red (NGO) — recordatorio:
//   • La lógica de este bot corre en el SERVIDOR (IsServer). Los clientes solo ven el resultado
//     replicado (NetworkTransform server-authoritative en bots).
//   • No activéis cámaras ni AudioListener en instancias que no sean del jugador local.
//
// Flujo recomendado para ampliar:
//   1) Añadir estados al enum BotState.
//   2) En Update (servidor), hacer transiciones según salud, distancia a enemigos, etc.
//   3) Delegar en m_Actions: moverse, mirar, disparar, cambiar arma…
// =================================================================================================

/// <summary>
/// Estados de alto nivel del bot. Ampliad el enum según vuestra práctica (Patrol, Chase, Flee…).
/// </summary>
public enum BotState
{
    /// <summary>Inactivo hasta que la red y el NavMesh estén listos.</summary>
    Idle,

    /// <summary>Comportamiento de ejemplo: deambular por el mapa eligiendo puntos aleatorios.</summary>
    Wandering,

    /// <summary>Podéis usar este estado cuando CurrentHealth &lt;= 0 o cuando queráis bloquear la IA.</summary>
    Dead
}

[RequireComponent(typeof(BotGameplayActions))]
[DisallowMultipleComponent]
public class FSM : NetworkBehaviour
{
    [Header("FSM — parámetros del ejemplo Wandering")]
    [Tooltip("Radio alrededor de la posición actual para elegir un nuevo punto aleatorio en NavMesh.")]
    [SerializeField] float m_WanderRadius = 25f;

    [Tooltip("Cada cuántos segundos, como máximo, se reconsidera el destino.")]
    [SerializeField] float m_RepathIntervalSeconds = 1.25f;

    [Header("FSM — depuración")]
    [SerializeField] bool m_LogStateTransitions;

    BotState m_State = BotState.Idle;
    BotState m_PreviousStateForLog;

    float m_NextRepathTime;

    Health m_Health;
    BotGameplayActions m_Actions;

    // ---------------------------------------------------------------------------------------------
    // Ciclo de vida red / componentes
    // ---------------------------------------------------------------------------------------------

    void Awake()
    {
        // El bot no debe competir con el teclado/ratón del jugador humano.
        // ALUMNOS: cuando implementéis disparo automático, podréis volver a habilitar
        // PlayerWeaponsManager desde BotGameplayActions.InitializeWeaponSystemsIfNeeded().
        DisableHumanInputAndWeaponStack();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        m_Actions = GetComponent<BotGameplayActions>();
        m_Health = GetComponent<Health>();

        if (m_Health != null)
        {
            m_Health.OnDie += OnBotDied;
            m_Health.OnHealed += OnBotHealed;
        }

        // Cámaras y listeners solo en el owner (en bots suele ser irrelevante, pero evita conflictos MPPM).
        if (!IsOwner)
            DisableCameraAndAudioForNonOwner();

        if (IsServer)
            EnterInitialStateOnServer();
    }

    public override void OnNetworkDespawn()
    {
        if (m_Health != null)
        {
            m_Health.OnDie -= OnBotDied;
            m_Health.OnHealed -= OnBotHealed;
        }

        base.OnNetworkDespawn();
    }

    void EnterInitialStateOnServer()
    {
        // Preparar navegación en servidor.
        m_Actions.EnsureNavMeshAgentReady();

        // Desactivamos el stack humano (CharacterController + PCC) para que no luche con NavMeshAgent.
        DisableHumanLocomotionThatConflictsWithNavMesh();

        TransitionTo(BotState.Wandering);
    }

    void DisableHumanInputAndWeaponStack()
    {
        var inputHandler = GetComponent<PlayerInputHandler>();
        if (inputHandler != null)
            inputHandler.enabled = false;

        var weapons = GetComponent<PlayerWeaponsManager>();
        if (weapons != null)
            weapons.enabled = false;
    }

    void DisableHumanLocomotionThatConflictsWithNavMesh()
    {
        var pcc = GetComponent<PlayerCharacterController>();
        if (pcc != null)
            pcc.enabled = false;

        var jetpack = GetComponent<Jetpack>();
        if (jetpack != null)
            jetpack.enabled = false;

        var playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
        if (playerInput != null)
            playerInput.enabled = false;

        var cc = GetComponent<CharacterController>();
        if (cc != null)
            cc.enabled = false;
    }

    void DisableCameraAndAudioForNonOwner()
    {
        foreach (var cam in GetComponentsInChildren<Camera>(true))
            cam.enabled = false;

        foreach (var listener in GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;

        foreach (var cam in GetComponentsInChildren<Camera>(true))
        {
            if (cam != null && cam.gameObject != null)
                cam.gameObject.SetActive(false);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Eventos de salud
    // ---------------------------------------------------------------------------------------------

    void OnBotDied()
    {
        TransitionTo(BotState.Dead);
        m_Actions.DisableNavMeshAgent();
    }

    void OnBotHealed(float _)
    {
        // Tras respawn, el servidor puede volver a mover al bot.
        m_Actions.EnableNavMeshAgent();
        m_NextRepathTime = 0f;
        TransitionTo(BotState.Wandering);
    }

    // ---------------------------------------------------------------------------------------------
    // Máquina de estados — núcleo
    // ---------------------------------------------------------------------------------------------

    void Update()
    {
        if (!IsServer)
            return;

        // Máquina de estados por frame (servidor).
        switch (m_State)
        {
            case BotState.Idle:
                // Nada: estado transitorio si no habéis llamado aún a EnterInitialStateOnServer.
                break;

            case BotState.Wandering:
                TickWanderingExample();
                break;

            case BotState.Dead:
                // No mover ni decidir: PlayerRespawner gestionará el revive en servidor.
                break;

                // ALUMNOS: añadir aquí case BotState.Chase: TickChase(); break;
        }
    }

    /// <summary>
    /// Ejemplo mínimo: deambular. Sustituidlo por lógica más rica (waypoints, Blackboard, Utility AI…).
    /// </summary>
    void TickWanderingExample()
    {
        if (m_Health != null && m_Health.CurrentHealth <= 0f)
            return;

        var agent = m_Actions.NavMeshAgent;
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        if (Time.time < m_NextRepathTime)
            return;

        m_NextRepathTime = Time.time + Mathf.Max(0.1f, m_RepathIntervalSeconds);

        bool needsNewTarget = !agent.hasPath || agent.pathPending || m_Actions.HasReachedCurrentDestination();
        if (!needsNewTarget)
            return;

        if (TryPickRandomNavMeshPoint(transform.position, Mathf.Max(2f, m_WanderRadius), out var dest))
            m_Actions.TryMoveToWorldPosition(dest);
    }

    /// <summary>
    /// Punto centralizado para transiciones. Usadlo para logging y tareas de entrada/salida de estado.
    /// </summary>
    void TransitionTo(BotState newState)
    {
        if (m_State == newState)
            return;

        OnExitState(m_State);
        m_PreviousStateForLog = m_State;
        m_State = newState;
        OnEnterState(newState);

        if (m_LogStateTransitions)
            Debug.Log($"[FSM] {name}: {m_PreviousStateForLog} -> {newState}", this);
    }

    void OnExitState(BotState state)
    {
        switch (state)
        {
            case BotState.Wandering:
                // Ej.: dejar de disparar, cancelar tweens…
                break;
        }
    }

    void OnEnterState(BotState state)
    {
        switch (state)
        {
            case BotState.Wandering:
                m_NextRepathTime = 0f;
                break;

            case BotState.Dead:
                m_Actions.StopNavigation();
                break;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Utilidades NavMesh (podrían moverse a BotGameplayActions si preferís cero lógica aquí)
    // ---------------------------------------------------------------------------------------------

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
