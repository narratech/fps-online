using System.Collections;
using UnityEngine;

namespace Unity.FPS.Game
{
    /// <summary>
    /// Objeto de escena persistente (DontDestroyOnLoad) que programa respawns locales.
    /// No depende de que el pickup/enemigo siga vivo tras Destroy: clona la plantilla al programar.
    /// </summary>
    public sealed class LocalRespawnService : MonoBehaviour
    {
        public static LocalRespawnService Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            EnsureExists();
        }

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("LocalRespawnService");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<LocalRespawnService>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Clona <paramref name="pickupInstance"/> inmediatamente (plantilla bajo padre inactivo), espera y spawnea en mundo.
        /// Llamar ANTES de Destroy(pickupInstance).
        /// </summary>
        public static void SchedulePickupCloneAfterDestroy(GameObject pickupInstance, float delaySeconds = 30f)
        {
            if (pickupInstance == null) return;
            EnsureExists();
            Instance.StartCoroutine(Instance.PickupRespawnRoutine(pickupInstance, delaySeconds));
        }

        /// <summary>Respawn desde un prefab del Project en posición/rotación fijas (sin clonar la instancia actual).</summary>
        public static void ScheduleProjectPrefabAt(GameObject projectPrefab, Vector3 position, Quaternion rotation,
            float delaySeconds, Transform parent)
        {
            if (projectPrefab == null) return;
            EnsureExists();
            Instance.StartCoroutine(
                Instance.ProjectPrefabRoutine(projectPrefab, position, rotation, delaySeconds, parent));
        }

        /// <summary>Respawn de enemigo desde prefab del Project (posición inicial guardada por el caller).</summary>
        public static void ScheduleEnemyPrefab(GameObject enemyPrefab, Vector3 position, Quaternion rotation,
            float delaySeconds, Transform parent)
        {
            ScheduleProjectPrefabAt(enemyPrefab, position, rotation, delaySeconds, parent);
        }

        IEnumerator PickupRespawnRoutine(GameObject src, float delaySeconds)
        {
            // Todo lo que sigue es síncrono antes del primer yield: src sigue vivo aquí.
            Vector3 pos = src.transform.position;
            Quaternion rot = src.transform.rotation;

            var holder = new GameObject("~LocalPickupTpl_" + src.GetInstanceID());
            holder.SetActive(false);
            DontDestroyOnLoad(holder);

            var template = Instantiate(src, holder.transform);
            template.name = src.name + "_RespawnTpl";
            template.SetActive(false);
            StripPickupForLocalRespawn(template);

            if (delaySeconds > 0f)
                yield return new WaitForSecondsRealtime(delaySeconds);

            if (template == null)
                yield break;

            var spawned = Instantiate(template, pos, rot);
            spawned.SetActive(true);
            Debug.Log($"[LocalRespawnService] Pickup respawneado localmente en {pos}", spawned);
        }

        IEnumerator ProjectPrefabRoutine(GameObject prefab, Vector3 position, Quaternion rotation, float delaySeconds,
            Transform parent)
        {
            if (delaySeconds > 0f)
                yield return new WaitForSecondsRealtime(delaySeconds);
            if (prefab == null)
                yield break;
            Transform safeParent = parent != null && parent ? parent : null;
            var spawned = Instantiate(prefab, position, rotation, safeParent);
            Debug.Log($"[LocalRespawnService] Objeto respawneado desde prefab en {position}", spawned);
        }

        static void StripPickupForLocalRespawn(GameObject root)
        {
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var t = mb.GetType();
                if (t.Name == "NetworkPickupSync")
                {
                    Destroy(mb);
                    continue;
                }

                if (t.Namespace == "Unity.Netcode")
                    Destroy(mb);
            }
        }
    }
}
