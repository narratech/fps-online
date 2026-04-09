using UnityEngine;

namespace Unity.FPS.Gameplay
{
    /// <summary>
    /// Opcional: asigna el prefab del pickup en mundo (el mismo que este objeto) para respawn local tras un delay.
    /// </summary>
    public class LocalWorldPickupRespawn : MonoBehaviour
    {
        [Tooltip("Prefab del pickup en el mundo (arrastra el mismo prefab que usas en la escena). Si está vacío, no hay respawn.")]
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
        /// Programa un clon del pickup en la posición/rotación actuales de este objeto.
        /// </summary>
        public void TryScheduleRespawnAtCurrentTransform()
        {
            if (m_WorldPickupPrefab == null) return;
            Unity.FPS.Game.LocalRespawnScheduler.Schedule(
                m_WorldPickupPrefab,
                transform.position,
                transform.rotation,
                m_DelaySeconds,
                transform.parent
            );
        }
    }
}
