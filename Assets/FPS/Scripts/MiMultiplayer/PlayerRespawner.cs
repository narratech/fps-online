using Unity.Netcode;
using UnityEngine;
using System.Collections;
using Unity.FPS.Game;

namespace Unity.FPS.Gameplay
{
    public class PlayerRespawner : NetworkBehaviour
    {
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

            // Si yo muero, yo inicio la cuenta atrás en mi pantalla (humanos) o como bot (sin UI).
            if (!IsOwner) return;

            if (GetComponent<FSM>() != null)
                StartCoroutine(BotDeathRoutine());
            else
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

        [ServerRpc]
        void RequestRespawnServerRpc(ServerRpcParams rpcParams = default)
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
            transform.position = spawnPos;
            transform.rotation = spawnRot;
            m_LastPlayerAttacker = null;
            if (m_Health != null) m_Health.Revive();

            // 3. Le responde ÚNICAMENTE al cliente que acaba de pedirlo
            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { rpcParams.Receive.SenderClientId } }
            };

            RespawnClientRpc(spawnPos, spawnRot, clientRpcParams);
        }

        [ClientRpc]
        void RespawnClientRpc(Vector3 spawnPosition, Quaternion spawnRotation, ClientRpcParams clientRpcParams = default)
        {
            // --- EL CLIENTE RENACE ---

            // 1. Apagamos el motor físico temporalmente para poder teletransportarnos
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            // 2. Nos movemos a la coordenada que nos dijo el servidor
            transform.position = spawnPosition;
            transform.rotation = spawnRotation;

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

            // Lista de jugadores actuales para evitar solapamiento (simple y barato).
            var players = Object.FindObjectsByType<PlayerCharacterController>(FindObjectsSortMode.None);
            const float minDistance = 6.0f;

            // Elegimos el punto "mejor" (más lejos de cualquier jugador existente).
            // Esto evita casos raros donde dos jugadores spawnean el mismo frame y el RNG elige igual.
            float bestMinDist = float.NegativeInfinity;
            GameObject best = null;
            for (int s = 0; s < spawnPoints.Length; s++)
            {
                var sp = spawnPoints[s];
                if (sp == null) continue;

                Vector3 candidate = sp.transform.position;
                float closest = float.PositiveInfinity;

                for (int i = 0; i < players.Length; i++)
                {
                    var p = players[i];
                    if (p == null) continue;
                    if (excludeSelf != null && p == excludeSelf)
                        continue;

                    float d = Vector3.Distance(p.transform.position, candidate);
                    if (d < closest) closest = d;
                }

                // Si no hay jugadores, closest queda +inf: este punto es válido.
                if (closest > bestMinDist)
                {
                    bestMinDist = closest;
                    best = sp;
                }
            }

            // Si el "mejor" aún está demasiado cerca, intentamos encontrar uno que cumpla el mínimo.
            if (best != null && bestMinDist >= minDistance)
            {
                spawnPos = best.transform.position;
                spawnRot = best.transform.rotation;
                return;
            }

            for (int s = 0; s < spawnPoints.Length; s++)
            {
                var sp = spawnPoints[s];
                if (sp == null) continue;

                Vector3 candidate = sp.transform.position;
                bool blocked = false;
                for (int i = 0; i < players.Length; i++)
                {
                    var p = players[i];
                    if (p == null) continue;
                    if (excludeSelf != null && p == excludeSelf)
                        continue;

                    if (Vector3.Distance(p.transform.position, candidate) < minDistance)
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

            // Fallback: el mejor que encontremos (aunque esté cerca).
            if (best != null)
            {
                spawnPos = best.transform.position;
                spawnRot = best.transform.rotation;
            }
        }
    }
}
