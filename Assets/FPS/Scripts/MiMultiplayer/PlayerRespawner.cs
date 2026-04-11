using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;

namespace Unity.FPS.Gameplay
{
    public class PlayerRespawner : NetworkBehaviour
    {
        [Header("Spawn en suelo")]
        [Tooltip("Capas contra las que proyectamos un rayo hacia abajo para alinear los pies al suelo (suelo/escenario).")]
        [SerializeField] LayerMask m_SpawnGroundLayers = Physics.DefaultRaycastLayers;

        [Tooltip("Distancia máxima del rayo hacia abajo desde el punto de spawn (marcador puede estar flotando).")]
        [SerializeField] float m_GroundSnapMaxDistance = 48f;

        [Tooltip("Pequeño offset por encima del suelo para evitar penetraciones que disparen overlap recovery (personaje 'subido' al techo).")]
        [SerializeField] float m_GroundSnapYOffset = 0.08f;

        private Health m_Health;
        private PlayerCharacterController m_CharacterController;
        PlayerNameTag m_LastPlayerAttacker;

        void Awake()
        {
            m_Health = GetComponent<Health>();
            m_CharacterController = GetComponent<PlayerCharacterController>();

            // Nos suscribimos a nuestra propia muerte
            if (m_Health != null)
            {
                m_Health.OnDie += HandleDeath;
                m_Health.OnDamaged += OnDamaged;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Spawn inicial aleatorio (solo servidor). El host a menudo spawnea antes de que el mapa
            // haya cargado los RespawnPoint → FindGameObjectsWithTag devuelve 0 y caíamos en (0,5,0).
            // Esperamos a que existan puntos (el cliente que entra después ya los tiene en escena).
            if (IsServer)
            {
                StartCoroutine(ServerInitialSpawnWhenPointsReady());
            }
        }

        IEnumerator ServerInitialSpawnWhenPointsReady()
        {
            // Stagger: reduce probabilidad de que 2 jugadores elijan el mismo RespawnPoint en el mismo frame.
            // (Especialmente en MPPM / host+cliente entrando casi a la vez.)
            yield return new WaitForSeconds(0.05f * (OwnerClientId % 5));

            const int maxWaits = 180; // ~3 s a 60 fps; suficiente para carga de escena
            for (int w = 0; w < maxWaits; w++)
            {
                var pts = GameObject.FindGameObjectsWithTag("RespawnPoint");
                if (pts != null && pts.Length > 0)
                {
                    ApplyRandomSpawnAtServer();
                    yield break;
                }

                yield return null;
            }

            // Último intento aunque sigan sin existir (mismo comportamiento que antes)
            ApplyRandomSpawnAtServer();
        }

        void ApplyRandomSpawnAtServer()
        {
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            TryMoveToRandomSpawnPointAvoidingPlayers();
            var grounded = transform.position;
            SnapSpawnPositionToGround(ref grounded);
            transform.position = grounded;

            Physics.SyncTransforms();
            if (cc != null) cc.enabled = true;
        }

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
            // Solo guardamos atacante si es un jugador (para evitar dar kills cuando te mata un bot/entorno).
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
                // Daño de bot/entorno/no-jugador: no debe dar kill a nadie.
                m_LastPlayerAttacker = null;
            }
        }

        void HandleDeath()
        {
            // El servidor contabiliza muertes/killers (si aplica)
            if (IsServer)
            {
                var myTag = GetComponent<PlayerNameTag>();
                if (myTag != null)
                    myTag.Deaths.Value++;

                if (m_LastPlayerAttacker != null && myTag != null && m_LastPlayerAttacker != myTag)
                    m_LastPlayerAttacker.Kills.Value++;
            }

            bool isBot = GetComponent<FSM>() != null;

            // Bot: el servidor siempre gestiona su respawn (dedicated o host).
            if (isBot)
            {
                if (IsServer)
                    StartCoroutine(BotDeathRoutineServer());
                return;
            }

            // Humano: el owner inicia la cuenta atrás (UI local) y pide respawn al servidor.
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

            // Le pedimos al servidor (Host) que nos busque un sitio para reaparecer
            RequestRespawnServerRpc();
        }

        IEnumerator BotDeathRoutine()
        {
            // Bot: misma latencia de respawn, pero sin tocar UI/cursor.
            yield return new WaitForSeconds(4f);
            RequestRespawnServerRpc();
        }

        IEnumerator BotDeathRoutineServer()
        {
            // En server dedicated el bot no tiene "owner" cliente que dispare el RPC.
            yield return new WaitForSeconds(4f);
            ServerPerformRespawn();
        }

        [ServerRpc]
        void RequestRespawnServerRpc(ServerRpcParams rpcParams = default)
        {
            ServerPerformRespawn();
        }

        void ServerPerformRespawn()
        {
            // 1. EL SERVIDOR: Busca todos los puntos de aparición en el mapa
            GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("RespawnPoint");
            Vector3 spawnPos = new Vector3(0, 5, 0); // Por si se te olvida poner puntos, caes del cielo
            Quaternion spawnRot = Quaternion.identity;

            // 2. Elige uno al azar evitando spawnear encima de otros jugadores, si es posible
            PickSpawnPointAvoidingPlayers(spawnPoints, m_CharacterController, out spawnPos, out spawnRot);

            // --- MUY IMPORTANTE ---
            // Este RPC solo responde al cliente que lo pidió, así que si NO revivimos también en el servidor,
            // el Health del servidor se queda "muerto" y deja de disparar OnDie en muertes posteriores.
            // (Eso rompe el contador de kills/muertes en partidas host+clientes).
            SnapSpawnPositionToGround(ref spawnPos);
            transform.position = spawnPos;
            transform.rotation = spawnRot;
            m_LastPlayerAttacker = null;
            if (m_Health != null) m_Health.Revive();

            Physics.SyncTransforms();

            // 3. Informamos a TODOS los clientes para que el respawn sea visible también en remotos.
            RespawnClientRpc(spawnPos, spawnRot);
        }

        [ClientRpc]
        void RespawnClientRpc(Vector3 spawnPosition, Quaternion spawnRotation, ClientRpcParams clientRpcParams = default)
        {
            // --- EL CLIENTE RENACE ---

            // 1. Apagamos el motor físico temporalmente para poder teletransportarnos
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            // 2. Nos movemos a la coordenada que nos dijo el servidor
            var pos = spawnPosition;
            SnapSpawnPositionToGround(ref pos);
            transform.position = pos;
            transform.rotation = spawnRotation;

            Physics.SyncTransforms();

            // 3. Volvemos a encender el motor físico
            if (cc != null) cc.enabled = true;

            // 4. Curamos la vida y reactivamos los controles
            if (m_Health != null) m_Health.Revive();
            if (m_CharacterController != null) m_CharacterController.OnRespawn();
        }

        void TryMoveToRandomSpawnPointAvoidingPlayers()
        {
            GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("RespawnPoint");
            PickSpawnPointAvoidingPlayers(spawnPoints, m_CharacterController, out var spawnPos, out var spawnRot);

            transform.position = spawnPos;
            transform.rotation = spawnRot;
        }

        /// <summary>
        /// Alinea la altura Y del spawn con el suelo bajo el marcador. Evita que el CharacterController,
        /// al reactivarse, use overlap recovery y empuje el cuerpo hacia arriba (sensación de estar pegado al "techo").
        /// </summary>
        void SnapSpawnPositionToGround(ref Vector3 worldPos)
        {
            var cc = GetComponent<CharacterController>();
            float footHeight = cc != null ? Mathf.Max(cc.skinWidth, m_GroundSnapYOffset) : m_GroundSnapYOffset;

            // Origen ligeramente por encima del marcador para no impactar en el suelo desde dentro del suelo.
            Vector3 origin = worldPos + Vector3.up * 0.35f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, m_GroundSnapMaxDistance, m_SpawnGroundLayers,
                    QueryTriggerInteraction.Ignore))
            {
                worldPos = new Vector3(worldPos.x, hit.point.y + footHeight, worldPos.z);
            }
        }

        /// <param name="excludeSelf">
        /// No tratar nuestra posición actual como "otro jugador" al comprobar solapes (evita que el host,
        /// aún en la posición por defecto del NetworkManager, bloquee todos los puntos cercanos al centro).
        /// </param>
        static void PickSpawnPointAvoidingPlayers(GameObject[] spawnPoints, PlayerCharacterController excludeSelf,
            out Vector3 spawnPos, out Quaternion spawnRot)
        {
            spawnPos = new Vector3(0, 5, 0);
            spawnRot = Quaternion.identity;

            if (spawnPoints == null || spawnPoints.Length == 0)
                return;

            var players = Object.FindObjectsByType<PlayerCharacterController>(FindObjectsSortMode.None);
            const float minDistance = 6.0f;

            // Orden aleatorio: si solo hay un jugador, antes el algoritmo determinista acababa siempre
            // eligiendo el mismo RespawnPoint (el "mejor" según recorrido del array).
            var indices = new List<int>(spawnPoints.Length);
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

            // 1) Entre los no bloqueados (≥ minDistance a otros), elegir uno al azar.
            var candidatesOk = new List<int>();
            for (int k = 0; k < indices.Count; k++)
            {
                int s = indices[k];
                var sp = spawnPoints[s];
                Vector3 candidate = sp.transform.position;
                if (IsSpawnBlockedByPlayers(candidate, players, excludeSelf, minDistance))
                    continue;
                candidatesOk.Add(s);
            }

            if (candidatesOk.Count > 0)
            {
                int pick = candidatesOk[Random.Range(0, candidatesOk.Count)];
                var chosen = spawnPoints[pick];
                spawnPos = chosen.transform.position;
                spawnRot = chosen.transform.rotation;
                return;
            }

            // 2) Si todos están "bloqueados", cualquier punto sirve: elige al azar entre todos.
            int fallback = indices[Random.Range(0, indices.Count)];
            {
                var sp = spawnPoints[fallback];
                spawnPos = sp.transform.position;
                spawnRot = sp.transform.rotation;
            }
        }

        static bool IsSpawnBlockedByPlayers(Vector3 candidateWorld, PlayerCharacterController[] players,
            PlayerCharacterController excludeSelf, float minDistance)
        {
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (p == null) continue;
                if (excludeSelf != null && p == excludeSelf)
                    continue;

                if (Vector3.Distance(p.transform.position, candidateWorld) < minDistance)
                    return true;
            }

            return false;
        }
    }
}
