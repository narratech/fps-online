using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Unity.FPS.Gameplay;
using Unity.FPS.Game;

// =================================================================================================
// FSM — Plantilla de máquina de estados SIMPLIFICADA Y A FUEGO EN EL CÓDIGO para UCM_Bot  
// =================================================================================================
// Objetivo:
//   • Separar "qué decide la IA" (esta clase) de "cómo se ejecutan las acciones en el juego"
//     (ver BotGameplayActions).
//
// Red (NGO) — Aclaraciones:
//   • La lógica de este bot corre en el SERVIDOR (IsServer). Los clientes solo ven el resultado
//     replicado (NetworkTransform server-authoritative en bots).
//   • No hay que activar cámaras ni AudioListener en instancias que no sean del jugador local.
//
// Vuestra tarea es escribir código aquí de una verdadera máquina de estados jerárquica, :
// que cargue la información de estados, transiciones, condiciones (según salud, según distancia a enemigos, etc.)
// y cuando haya que realizar alguna acción delegar en m_Actions.
// =================================================================================================

/// <summary>
/// Estados de alto nivel del bot (ej. Patrol, Chase, Flee…).
/// </summary>
public enum BotState
{
    /// <summary>Inactivo hasta que la red y el NavMesh estén listos.</summary>
    Idle,

    /// <summary>Comportamiento de ejemplo: deambular por el mapa eligiendo puntos aleatorios.</summary>
    Wandering,

    /// <summary>Podéis usar este estado cuando CurrentHealth &lt;= 0 o cuando queráis bloquear la IA por otra razón.</summary>
    Dead
}

[RequireComponent(typeof(BotGameplayActions))]
[DisallowMultipleComponent]
/// <summary>
/// Máquina de estados simple del bot basada en NGO.
/// <para>
/// Autoridad: <b>servidor</b>. El <see cref="Update"/> de IA retorna en clientes para que el bot no se "duplique".
/// </para>
/// <para>
/// Dependencias:
/// - <see cref="BotGameplayActions"/> ejecuta movimiento/armas y encapsula detalles de componentes del FPS sample.
/// - <see cref="Health"/> emite eventos para transicionar a <see cref="BotState.Dead"/> y reactivar tras revive.
/// </para>
/// <para>
/// DEFECTUOSO (búsquedas por frame potenciales): este script usa <c>Object.FindFirstObjectByType</c> en init
/// (solo al inicio). Está bien para prototipo, pero en builds grandes conviene inyectar referencias.
/// </para>
/// </summary>
public class FSM : NetworkBehaviour
{
    [Header("FSM — parámetros del ejemplo Wandering")]
    [Tooltip("Radio alrededor de la posición actual para elegir un nuevo punto aleatorio en NavMesh.")]
    /// <summary>Radio máximo para elegir puntos de deambular.</summary>
    [SerializeField] float m_WanderRadius = 25f;

    [Tooltip("Cada cuántos segundos, como máximo, se reconsidera el destino.")]
    /// <summary>Cadencia con la que se recalcula un nuevo destino de wandering.</summary>
    [SerializeField] float m_RepathIntervalSeconds = 1.25f;

    [Header("FSM — depuración")]
    /// <summary>Si true, escribe en consola transiciones de estado.</summary>
    [SerializeField] bool m_LogStateTransitions;

    /// <summary>Estado actual.</summary>
    BotState m_State = BotState.Idle;
    /// <summary>Estado anterior solo para logging.</summary>
    BotState m_PreviousStateForLog;

    /// <summary>Timestamp (Time.time) del próximo repath permitido.</summary>
    float m_NextRepathTime;

    /// <summary>Referencia a vida del bot para gating y eventos.</summary>
    Health m_Health;
    /// <summary>API de acciones (movimiento/armas) usada por la FSM.</summary>
    BotGameplayActions m_Actions;

    // ---------------------------------------------------------------------------------------------
    // Ciclo de vida red / componentes
    // ---------------------------------------------------------------------------------------------

    void Awake()
    {
        // El bot no debe competir con el teclado/ratón del jugador humano.
        // Pista: cuando implementéis disparo automático, podréis volver a habilitar
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

        // Cámaras y listeners solo en el owner (en bots suele ser irrelevante, pero evita conflictos de tipo MPPM).
        if (!IsOwner)
            DisableCameraAndAudioForNonOwner();

        if (IsServer)
            StartCoroutine(ServerInitBotWhenGameplaySceneReady());
    }

    public override void OnNetworkDespawn()
    {
        StopAllCoroutines();

        if (m_Health != null)
        {
            m_Health.OnDie -= OnBotDied;
            m_Health.OnHealed -= OnBotHealed;
        }

        base.OnNetworkDespawn();
    }

    /// <summary>
    /// El host spawnea el player object en cuanto arranca la red; la escena de juego (NavMesh, ActorsManager)
    /// carga justo después. Esperamos a que existan antes de crear el NavMeshAgent.
    /// </summary>
    IEnumerator ServerInitBotWhenGameplaySceneReady()
    {
        // Espera activa hasta que la escena de gameplay tenga NavMesh y exista ActorsManager.
        // Esto evita añadir/activar NavMeshAgent en escenas de menú (sin NavMesh).
        const float timeoutSeconds = 45f;
        float elapsed = 0f;

        while (elapsed < timeoutSeconds)
        {
            bool navReady = BotGameplayActions.SceneHasNavMeshData();
            bool actorsReady = Object.FindFirstObjectByType<ActorsManager>() != null;

            if (navReady && actorsReady)
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        m_Actions.EnsureNavMeshAgentReady();

        DisableHumanLocomotionThatConflictsWithNavMesh();

        TransitionTo(BotState.Wandering);
    }

    void DisableHumanInputAndWeaponStack()
    {
        // Desactiva input/armas del stack humano para evitar competencia con IA.
        var inputHandler = GetComponent<PlayerInputHandler>();
        if (inputHandler != null)
            inputHandler.enabled = false;

        var weapons = GetComponent<PlayerWeaponsManager>();
        if (weapons != null)
            weapons.enabled = false;
    }

    void DisableHumanLocomotionThatConflictsWithNavMesh()
    {
        // Desactiva locomoción "humana" (CC/PCC/Jetpack) para que NavMeshAgent gobierne el movimiento.
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
        // En bots/objetos no controlados localmente, desactivamos cámaras y audio para evitar:
        // - múltiples AudioListener,
        // - cámaras extra renderizando,
        // - costes de rendimiento.
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
        // Evento de Health: transiciona y detiene navegación (servidor).
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
    // Máquina de estados — Esto viene a ser núcleo de la IA, lo que tendréis que cambiar.
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

                // Aquí se podrían añadir otros estados como BotState.Chase: TickChase(); break;
        }
    }

    /// <summary>
    /// Ejemplo mínimo: deambular. Es un ejemplo sencillote que deberá sustituirse por lógica más rica de una verdadera máquina de estados jerárquica.
    /// </summary>
    void TickWanderingExample()
    {
        // Ejemplo mínimo. La IA real debería modular:
        // - selección de objetivos,
        // - transiciones por estímulos (ruido, visión, daño),
        // - combate,
        // - respawn/inactividad.
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
        // Punto único para transiciones: útil para ejecutar on-enter/on-exit y logging.
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
    // Utilidades NavMesh (podrían moverse a BotGameplayActions si preferís no tener nada de lógica aquí)
    // ---------------------------------------------------------------------------------------------

    static bool TryPickRandomNavMeshPoint(Vector3 origin, float radius, out Vector3 result)
    {
        // Muestreo aleatorio: intenta 20 candidatos en esfera y proyecta en NavMesh.
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
