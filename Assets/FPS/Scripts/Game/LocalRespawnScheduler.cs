using System.Collections;
using UnityEngine;

namespace Unity.FPS.Game
{
    /// <summary>
    /// Programa respawns locales (solo esta máquina) sin pasar por el servidor.
    /// Usa un host DontDestroyOnLoad para seguir corriendo corrutinas tras destruir el objeto origen.
    /// </summary>
    public static class LocalRespawnScheduler
    {
        class HostBehaviour : MonoBehaviour
        {
        }

        static HostBehaviour s_Host;

        static void EnsureHost()
        {
            if (s_Host != null) return;
            var go = new GameObject("[LocalRespawnScheduler]");
            Object.DontDestroyOnLoad(go);
            s_Host = go.AddComponent<HostBehaviour>();
        }

        /// <param name="parent">Opcional; si es null, el objeto queda en la raíz de la escena activa.</param>
        public static void Schedule(GameObject prefab, Vector3 position, Quaternion rotation, float delaySeconds,
            Transform parent = null)
        {
            if (prefab == null || delaySeconds < 0f) return;
            EnsureHost();
            s_Host.StartCoroutine(SpawnAfterDelay(prefab, position, rotation, delaySeconds, parent));
        }

        static IEnumerator SpawnAfterDelay(GameObject prefab, Vector3 position, Quaternion rotation, float delaySeconds,
            Transform parent)
        {
            if (delaySeconds > 0f)
                yield return new WaitForSeconds(delaySeconds);
            Object.Instantiate(prefab, position, rotation, parent);
        }
    }
}
