using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    /// <summary>
    /// Opcional en el prefab: delay y/o prefab del Project para el respawn local.
    /// Si no hay componente, <see cref="Unity.FPS.Game.LocalRespawnService"/> usa 30 s y clona la instancia actual.
    /// </summary>
    public class LocalWorldPickupRespawn : MonoBehaviour
    {
        [Tooltip("Opcional: arrastra el prefab desde la carpeta Project (no la instancia en la escena).")]
        [SerializeField] GameObject m_WorldPickupPrefab;

        [Tooltip("Segundos hasta que vuelve a aparecer (solo en esta máquina).")]
        [SerializeField] float m_DelaySeconds = 30f;

        public float DelaySeconds
        {
            get => m_DelaySeconds;
            set => m_DelaySeconds = value;
        }

        public void SetWorldPrefab(GameObject prefab)
        {
            m_WorldPickupPrefab = prefab;
        }

        /// <summary>
        /// Programa respawn local ANTES de Destroy. Siempre llama esto (o el servicio directamente).
        /// </summary>
        public void ScheduleBeforeDestroy()
        {
            if (m_WorldPickupPrefab != null && !m_WorldPickupPrefab.scene.IsValid())
            {
                LocalRespawnService.ScheduleProjectPrefabAt(m_WorldPickupPrefab, transform.position,
                    transform.rotation, m_DelaySeconds, transform.parent);
                return;
            }

            LocalRespawnService.SchedulePickupCloneAfterDestroy(gameObject, m_DelaySeconds);
        }

        /// <summary>
        /// Usar desde pickups: si hay <see cref="LocalWorldPickupRespawn"/>, aplica delay/prefab; si no, 30 s y clon de la instancia.
        /// </summary>
        public static void ScheduleLocalRespawnFor(GameObject pickup)
        {
            if (pickup == null) return;
            var lr = pickup.GetComponent<LocalWorldPickupRespawn>();
            if (lr != null)
                lr.ScheduleBeforeDestroy();
            else
                LocalRespawnService.SchedulePickupCloneAfterDestroy(pickup, 30f);
        }
    }
}
