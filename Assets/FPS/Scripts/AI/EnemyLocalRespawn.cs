using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.AI
{
    /// <summary>
    /// Opcional: tras morir el enemigo, vuelve a instanciarse localmente tras un delay (misma posición inicial).
    /// Asigna el mismo prefab de enemigo que quieras respawnear.
    /// </summary>
    public class EnemyLocalRespawn : MonoBehaviour
    {
        [Tooltip("Mismo prefab del enemigo (HoverBot, Turret, etc.). Si está vacío, no hay respawn.")]
        [SerializeField] GameObject m_EnemyPrefab;

        [SerializeField] float m_DelaySeconds = 30f;

        Vector3 m_InitialPosition;
        Quaternion m_InitialRotation;
        Transform m_Parent;

        void Awake()
        {
            m_InitialPosition = transform.position;
            m_InitialRotation = transform.rotation;
            m_Parent = transform.parent;
        }

        /// <summary>Llamado desde EnemyController al morir (antes de Destroy).</summary>
        public void OnEnemyDiedScheduleLocalRespawn()
        {
            if (m_EnemyPrefab == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(
                    "[EnemyLocalRespawn] m_EnemyPrefab está vacío: no hay respawn local. Asigna el mismo prefab del enemigo en el inspector.",
                    this);
#endif
                return;
            }

            LocalRespawnService.ScheduleEnemyPrefab(m_EnemyPrefab, m_InitialPosition, m_InitialRotation, m_DelaySeconds,
                m_Parent);
        }
    }
}
