using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;

namespace Unity.FPS.Gameplay
{
    /// <summary>
    /// Respawn sincronizado por servidor para jugadores y bots (NGO).
    /// <para>
    /// Flujo:
    /// - Al morir (<see cref="Health.OnDie"/>), el servidor incrementa stats (kills/deaths) y decide respawn.
    /// - Humanos: el owner (cliente local) espera unos segundos y solicita respawn con <see cref="RequestRespawnServerRpc"/>.
    /// - Bots: el servidor espera y respawnea directamente (sin pedir permiso al owner).
    /// - El servidor teletransporta al objeto y manda a clientes <see cref="RespawnClientRpc"/> para aplicar
    ///   el mismo cambio (deshabilitando temporalmente CharacterController para evitar colisiones raras).
    /// </para>
    /// <para>
    /// Nota: además del teletransporte, revive <see cref="Health"/> y notifica al <see cref="PlayerCharacterController"/>.
    /// </para>
    /// </summary>
    public class PlayerRespawner : NetworkBehaviour
    {
        [Header("Spawn — puntos RespawnPoint")]
        [Tooltip("Separación mínima en el suelo (XZ) respecto a otros jugadores para considerar un punto \"libre\".")]
        /// <summary>Distancia mínima horizontal a otros jugadores para considerar un respawn "seguro".</summary>
        [SerializeField] float m_MinHorizontalSeparationFromPlayers = 3f;

        [Tooltip("Radio usado solo en el fallback NavMesh: proyectar el marcador sobre la malla cerca del suelo.")]
        /// <summary>Radio de muestreo al proyectar un RespawnPoint sobre NavMesh (fallback).</summary>
        [SerializeField] float m_NavMeshSampleRadius = 4f;

        /// <summary>Vida del personaje (para escuchar muerte/daño).</summary>
        private Health m_Health;
        /// <summary>Controlador del personaje para callback de respawn.</summary>
        private PlayerCharacterController m_CharacterController;
        /// <summary>Último atacante (para atribuir kill). Se resuelve desde DamageSource.</summary>
        PlayerNameTag m_LastPlayerAttacker;

        /// <summary>Cache de referencias y suscripción a eventos locales.</summary>
        void Awake()
        {
            m_Health = GetComponent<Health>();
            m_CharacterController = GetComponent<PlayerCharacterController>();

            if (m_Health != null)
            {
                m_Health.OnDie += HandleDeath;
                m_Health.OnDamaged += OnDamaged;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
                StartCoroutine(ServerInitialSpawnWhenPointsReady());
        }

        /// <summary>
        /// Spawn inicial: espera a que existan objetos con tag `RespawnPoint` para evitar caer al vacío.
        /// </summary>
        IEnumerator ServerInitialSpawnWhenPointsReady()
        {
            yield return new WaitForSeconds(0.05f * (OwnerClientId % 5));

            const int maxWaits = 180;
            for (int w = 0; w < maxWaits; w++)
            {
                var pts = GameObject.FindGameObjectsWithTag("RespawnPoint");
                if (pts != null && pts.Length > 0)
                {
                    ApplyInitialSpawnAtServer();
                    yield break;
                }

                yield return null;
            }

            ApplyInitialSpawnAtServer();
        }

        /// <summary>
        /// Teletransporta al jugador en servidor a un RespawnPoint (desactivando CC temporalmente).
        /// </summary>
        void ApplyInitialSpawnAtServer()
        {
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            var spawnPoints = GameObject.FindGameObjectsWithTag("RespawnPoint");
            PickSpawnTransform(spawnPoints, m_CharacterController, out var spawnPos, out var spawnRot);

            transform.position = spawnPos;
            transform.rotation = spawnRot;

            Physics.SyncTransforms();
            if (cc != null) cc.enabled = true;
        }

        /// <summary>Desuscripción defensiva de eventos.</summary>
        public override void OnDestroy()
        {
            base.OnDestroy();
            if (m_Health != null)
            {
                m_Health.OnDie -= HandleDeath;
                m_Health.OnDamaged -= OnDamaged;
            }
        }

        void OnDamaged(float damage, GameObject damageSource)
        {
            // Track del último atacante para atribución de kill (solo si es otro jugador).
            if (damageSource == null)
            {
                m_LastPlayerAttacker = null;
                return;
            }

            var attackerTag = damageSource.GetComponentInParent<PlayerNameTag>();
            if (attackerTag != null && attackerTag != GetComponent<PlayerNameTag>())
            {
                m_LastPlayerAttacker = attackerTag;
            }
            else
            {
                m_LastPlayerAttacker = null;
            }
        }

        void HandleDeath()
        {
            // En servidor: contabiliza muertes y kills (si hay atacante distinto).
            if (IsServer)
            {
                var myTag = GetComponent<PlayerNameTag>();
                if (myTag != null)
                    myTag.Deaths.Value++;

                if (m_LastPlayerAttacker != null && myTag != null && m_LastPlayerAttacker != myTag)
                    m_LastPlayerAttacker.Kills.Value++;
            }

            bool isBot = GetComponent<FSM>() != null;

            if (isBot)
            {
                // Bot: el servidor gestiona todo el ciclo de muerte/respawn sin intervención de un owner "humano".
                if (IsServer)
                    StartCoroutine(BotDeathRoutineServer());
                return;
            }

            // Humano: solo el owner muestra delay/fade local y solicita respawn al servidor.
            if (!IsOwner) return;
            StartCoroutine(DeathRoutine());
        }

        IEnumerator DeathRoutine()
        {
            GameFlowManager flowManager = FindFirstObjectByType<GameFlowManager>();
            CanvasGroup fadeCanvas = flowManager != null ? flowManager.EndGameFadeCanvasGroup : null;

            if (fadeCanvas != null && fadeCanvas.gameObject != null)
                fadeCanvas.gameObject.SetActive(false);

            yield return new WaitForSeconds(4f);

            if (fadeCanvas != null && fadeCanvas.gameObject != null)
                fadeCanvas.gameObject.SetActive(true);

            RequestRespawnServerRpc();
        }

        IEnumerator BotDeathRoutine()
        {
            yield return new WaitForSeconds(4f);
            RequestRespawnServerRpc();
        }

        IEnumerator BotDeathRoutineServer()
        {
            yield return new WaitForSeconds(4f);
            ServerPerformRespawn();
        }

        [ServerRpc]
        /// <summary>RPC owner→servidor: solicita respawn (servidor decide el punto y revive).</summary>
        void RequestRespawnServerRpc(ServerRpcParams rpcParams = default)
        {
            ServerPerformRespawn();
        }

        /// <summary>
        /// Respawn autoritativo en servidor: elige punto, teletransporta, revive, y notifica a clientes.
        /// </summary>
        void ServerPerformRespawn()
        {
            GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("RespawnPoint");
            Vector3 spawnPos = new Vector3(0, 5, 0);
            Quaternion spawnRot = Quaternion.identity;

            PickSpawnTransform(spawnPoints, m_CharacterController, out spawnPos, out spawnRot);

            transform.position = spawnPos;
            transform.rotation = spawnRot;
            m_LastPlayerAttacker = null;
            if (m_Health != null) m_Health.Revive();

            Physics.SyncTransforms();

            RespawnClientRpc(spawnPos, spawnRot);
        }

        [ClientRpc]
        /// <summary>
        /// RPC servidor→clientes: aplica el respawn en instancias remotas (teletransporte + revive).
        /// </summary>
        void RespawnClientRpc(Vector3 spawnPosition, Quaternion spawnRotation, ClientRpcParams clientRpcParams = default)
        {
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            transform.position = spawnPosition;
            transform.rotation = spawnRotation;

            Physics.SyncTransforms();

            if (cc != null) cc.enabled = true;

            if (m_Health != null) m_Health.Revive();
            if (m_CharacterController != null) m_CharacterController.OnRespawn();
        }

        /// <summary>
        /// 1) Barajar los RespawnPoint; usar el primero cuyo suelo (XZ) esté libre de otros jugadores.
        /// 2) Si todos parecen ocupados: proyectar cada marcador sobre NavMesh y tomar el primero válido.
        /// 3) Si aun así no hay malla: usar posición/rotación del marcador tal cual (fallback).
        /// </summary>
        void PickSpawnTransform(GameObject[] spawnPoints, PlayerCharacterController excludeSelf,
            out Vector3 spawnPos, out Quaternion spawnRot)
        {
            // Algoritmo documentado en el summary: intenta "libres", luego NavMesh, luego fallback.
            spawnPos = new Vector3(0, 5, 0);
            spawnRot = Quaternion.identity;

            if (spawnPoints == null || spawnPoints.Length == 0)
                return;

            var players = Object.FindObjectsByType<PlayerCharacterController>(FindObjectsSortMode.None);
            float minSep = Mathf.Max(0.5f, m_MinHorizontalSeparationFromPlayers);

            var indices = new List<int>();
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] != null)
                    indices.Add(i);
            }

            if (indices.Count == 0)
                return;

            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            // --- Paso 1: marcadores libres (solo distancia horizontal entre jugadores) ---
            for (int k = 0; k < indices.Count; k++)
            {
                var sp = spawnPoints[indices[k]];
                Vector3 candidate = sp.transform.position;
                if (IsXZTooCloseToOtherPlayers(candidate, players, excludeSelf, minSep))
                    continue;

                spawnPos = sp.transform.position;
                spawnRot = sp.transform.rotation;
                return;
            }

            // --- Paso 2: todos ocupados → punto en NavMesh cerca de un marcador (barajado) ---
            for (int k = 0; k < indices.Count; k++)
            {
                var sp = spawnPoints[indices[k]];
                Vector3 p = sp.transform.position;
                if (NavMesh.SamplePosition(p, out var hit, m_NavMeshSampleRadius, NavMesh.AllAreas))
                {
                    spawnPos = hit.position;
                    spawnRot = sp.transform.rotation;
                    return;
                }
            }

            // --- Paso 3: último recurso, primer marcador barajado ---
            var fallback = spawnPoints[indices[0]];
            spawnPos = fallback.transform.position;
            spawnRot = fallback.transform.rotation;
        }

        static bool IsXZTooCloseToOtherPlayers(Vector3 candidateWorld, PlayerCharacterController[] players,
            PlayerCharacterController excludeSelf, float minHorizontal)
        {
            // Métrica solo en XZ (plano suelo), ignorando altura.
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (p == null) continue;
                if (excludeSelf != null && p == excludeSelf)
                    continue;

                Vector3 o = p.transform.position;
                float dx = candidateWorld.x - o.x;
                float dz = candidateWorld.z - o.z;
                if (dx * dx + dz * dz < minHorizontal * minHorizontal)
                    return true;
            }

            return false;
        }
    }
}
