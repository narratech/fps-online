using UnityEngine;

namespace Unity.FPS.Gameplay
{
    /// <summary>
    /// Respawn local tras un delay. Si asignas el prefab del proyecto en "World Pickup Prefab", se usa ese.
    /// Si no (o si arrastraste la instancia de escena), se genera una plantilla en Awake sin componentes de Netcode
    /// para poder instanciar sin ServerRpc ni referencias rotas.
    /// </summary>
    public class LocalWorldPickupRespawn : MonoBehaviour
    {
        [Tooltip("Opcional: arrastra el prefab desde la carpeta Project (no la instancia en la escena).")]
        [SerializeField] GameObject m_WorldPickupPrefab;

        [Tooltip("Segundos hasta que vuelve a aparecer (solo en esta máquina).")]
        [SerializeField] float m_DelaySeconds = 30f;

        GameObject m_LocalSpawnTemplate;

        public float DelaySeconds
        {
            get => m_DelaySeconds;
            set => m_DelaySeconds = value;
        }

        public void SetWorldPrefab(GameObject prefab)
        {
            m_WorldPickupPrefab = prefab;
        }

        void Awake()
        {
            if (gameObject.name.EndsWith("_LocalRespawnTemplate", System.StringComparison.Ordinal))
                return;

            BuildHiddenLocalTemplate();
        }

        void BuildHiddenLocalTemplate()
        {
            if (m_LocalSpawnTemplate != null) return;

            // Padre inactivo: el clon no recibe Awake/Start hasta que exista; evita recursión al duplicar este GO.
            var holder = new GameObject("~PickupRespawnHolder_" + gameObject.GetInstanceID());
            holder.SetActive(false);
            Object.DontDestroyOnLoad(holder);

            m_LocalSpawnTemplate = Instantiate(gameObject, holder.transform);
            m_LocalSpawnTemplate.name = gameObject.name + "_LocalRespawnTemplate";
            m_LocalSpawnTemplate.SetActive(false);

            StripNetcodeAndNetworkPickupSync(m_LocalSpawnTemplate);
        }

        static void StripNetcodeAndNetworkPickupSync(GameObject root)
        {
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var t = mb.GetType();
                if (t.Name == "NetworkPickupSync")
                {
                    Object.Destroy(mb);
                    continue;
                }

                var ns = t.Namespace;
                if (ns == "Unity.Netcode")
                    Object.Destroy(mb);
            }
        }

        /// <summary>
        /// Programa un clon del pickup en la posición/rotación actuales de este objeto.
        /// </summary>
        public void TryScheduleRespawnAtCurrentTransform()
        {
            Vector3 pos = transform.position;
            Quaternion rot = transform.rotation;
            Transform parent = transform.parent;

            // 1) Prefab de proyecto (no pertenece a una escena cargada como instancia)
            if (m_WorldPickupPrefab != null && !m_WorldPickupPrefab.scene.IsValid())
            {
                Unity.FPS.Game.LocalRespawnScheduler.Schedule(m_WorldPickupPrefab, pos, rot, m_DelaySeconds, parent);
                return;
            }

            // 2) Plantilla local (misma jerarquía que este pickup, sin Netcode)
            if (m_LocalSpawnTemplate != null)
            {
                Unity.FPS.Game.LocalRespawnScheduler.Schedule(m_LocalSpawnTemplate, pos, rot, m_DelaySeconds, null);
                return;
            }

            if (m_WorldPickupPrefab != null && m_WorldPickupPrefab.scene.IsValid())
            {
                Debug.LogWarning(
                    "[LocalWorldPickupRespawn] Asigna el prefab desde el Project, o deja el campo vacío para usar la plantilla automática.",
                    this);
            }
        }
    }
}
