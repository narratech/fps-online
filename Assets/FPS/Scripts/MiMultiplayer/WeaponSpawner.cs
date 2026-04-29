using Unity.Netcode;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    /// <summary>
    /// Spawner de pickups de armas sincronizado por servidor (NGO).
    /// <para>
    /// Autoridad:
    /// - Solo el servidor instancia y hace <c>Spawn()</c> del `NetworkObject`.
    /// - Los clientes solo reciben la aparición vía replicación.
    /// </para>
    /// </summary>
    public class WeaponSpawner : NetworkBehaviour
    {
        [Header("Configuración del Spawner")]
        [Tooltip("Lista de los prefabs de los Pickups de armas (Deben tener NetworkObject)")]
        /// <summary>
        /// Prefabs posibles a instanciar. Cada uno debe incluir <see cref="NetworkObject"/> y estar registrado en
        /// las listas de prefabs de NGO.
        /// </summary>
        public GameObject[] WeaponPickups;

        [Tooltip("Tiempo en segundos entre cada aparición")]
        /// <summary>
        /// Intervalo entre spawns cuando no hay pickup activo.
        /// </summary>
        public float SpawnInterval = 15f;

        // Referencia al arma que está actualmente flotando
        /// <summary>
        /// Referencia al `NetworkObject` del pickup actualmente activo (si existe y sigue spawn).
        /// </summary>
        private NetworkObject currentSpawnedWeapon;
        /// <summary>
        /// Temporizador interno en segundos (solo corre en servidor).
        /// </summary>
        private float timer;

        /// <summary>
        /// Callback de NGO cuando el spawner aparece en red.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            // Solo el servidor controla cuándo aparecen las armas
            if (IsServer)
            {
                timer = SpawnInterval; // Empezamos a contar
            }
        }

        /// <summary>
        /// Tick servidor: cuenta y spawnea cuando toca, siempre que no haya pickup activo.
        /// </summary>
        void Update()
        {
            // Si no somos el servidor, no hacemos nada
            if (!IsServer) return;

            // Si ya hay un arma flotando en este spawner, pausamos el temporizador
            if (currentSpawnedWeapon != null && currentSpawnedWeapon.IsSpawned)
                return;

            // Restamos tiempo
            timer -= Time.deltaTime;

            if (timer <= 0f)
            {
                SpawnRandomWeapon();
                timer = SpawnInterval; // Reiniciamos el contador
            }
        }

        /// <summary>
        /// Instancia un pickup aleatorio y lo spawnea en red.
        /// </summary>
        void SpawnRandomWeapon()
        {
            if (WeaponPickups.Length == 0) return;

            // 1. Elegimos un arma al azar
            int randomIndex = Random.Range(0, WeaponPickups.Length);
            GameObject weaponPrefab = WeaponPickups[randomIndex];

            // 2. La instanciamos físicamente en la posición del spawner
            GameObject spawnedObject = Instantiate(weaponPrefab, transform.position, transform.rotation);

            // 3. Le decimos al servidor que la haga aparecer en todas las pantallas
            currentSpawnedWeapon = spawnedObject.GetComponent<NetworkObject>();
            if (currentSpawnedWeapon != null)
            {
                currentSpawnedWeapon.Spawn();
            }
            else
            {
                Debug.LogError("¡El arma " + weaponPrefab.name + " no tiene el componente NetworkObject!");
            }
        }
    }
}