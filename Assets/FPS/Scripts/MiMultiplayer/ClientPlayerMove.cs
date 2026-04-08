using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using Unity.FPS.Gameplay;
using Unity.FPS.Game;
using UnityEngine.SceneManagement; // ¡NUEVO! Necesario para saber cuándo carga la escena

public class NewMonoBehaviourScript : NetworkBehaviour
{
    [SerializeField] private PlayerInputHandler m_playerInputHandler;
    [SerializeField] private PlayerCharacterController m_playerCharacterController;
    [SerializeField] private Health m_Health;
    [SerializeField] private PlayerWeaponsManager m_WeaponsManager;
    [SerializeField] private Jetpack m_jetpack;
    [SerializeField] private CharacterController m_characterController;
    [SerializeField] private Actor m_actor;
    [SerializeField] private Damageable m_damageable;
    [SerializeField] private PlayerInput m_playerinput;
    [SerializeField] private GameObject m_camera;

    [Header("Gameplay Scenes")]
    [Tooltip("Escenas donde el jugador debe activarse (cámara, input, controller, etc.).")]
    [SerializeField] private string[] gameplaySceneNames = { "MainScene", "PrisonScene", "SecondaryScene" };

    void Awake()
    {
        // Apagamos todo al nacer para que no caiga al vacío en el Menú
        if (m_camera != null) m_camera.SetActive(false);
        if (m_playerInputHandler != null) m_playerInputHandler.enabled = false;
        if (m_playerCharacterController != null) m_playerCharacterController.enabled = false;
        if (m_Health != null) m_Health.enabled = false;
        if (m_WeaponsManager != null) m_WeaponsManager.enabled = false;
        if (m_jetpack != null) m_jetpack.enabled = false;
        if (m_characterController != null) m_characterController.enabled = false;
        if (m_actor != null) m_actor.enabled = false;
        if (m_damageable != null) m_damageable.enabled = false;
        if (m_playerinput != null) m_playerinput.enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            // 1. Nos suscribimos al evento "Cuando una escena termine de cargar"
            SceneManager.sceneLoaded += OnSceneLoaded;

            // 2. Si por algún motivo ya estamos en una escena jugable al nacer
            if (EsEscenaJugable(SceneManager.GetActiveScene().name))
            {
                UbicarYEncenderJugador();
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        // Limpieza de memoria súper importante al destruir el objeto
        if (IsOwner)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Solo reaccionamos si la escena que acaba de cargar es jugable
        if (EsEscenaJugable(scene.name))
        {
            UbicarYEncenderJugador();
        }
    }

    private bool EsEscenaJugable(string sceneName)
    {
        if (gameplaySceneNames == null || gameplaySceneNames.Length == 0) return false;
        for (int i = 0; i < gameplaySceneNames.Length; i++)
        {
            if (gameplaySceneNames[i] == sceneName) return true;
        }

        return false;
    }

    private void UbicarYEncenderJugador()
    {
        // 1. Buscamos nuestro punto de aparición por su Tag
        GameObject spawnPoint = GameObject.FindWithTag("Respawn");

        // 2. Nos aseguramos de que el CharacterController esté apagado antes de moverlo
        if (m_characterController != null) m_characterController.enabled = false;

        // 3. Teletransporte
        if (spawnPoint != null)
        {
            transform.position = spawnPoint.transform.position;
            transform.rotation = spawnPoint.transform.rotation;
        }
        else
        {
            // Por si se te olvida poner el SpawnPoint, lo dejamos caer desde el cielo
            transform.position = new Vector3(0, 5f, 0);
        }

        // 4. ¡Encendemos todo en el mapa correcto!
        if (m_camera != null) m_camera.SetActive(true);
        if (m_playerInputHandler != null) m_playerInputHandler.enabled = true;
        if (m_playerCharacterController != null) m_playerCharacterController.enabled = true;
        if (m_Health != null) m_Health.enabled = true;
        if (m_WeaponsManager != null) m_WeaponsManager.enabled = true;
        if (m_jetpack != null) m_jetpack.enabled = true;
        if (m_characterController != null) m_characterController.enabled = true;
        if (m_actor != null) m_actor.enabled = true;
        if (m_damageable != null) m_damageable.enabled = true;
        if (m_playerinput != null) m_playerinput.enabled = true;
    }
}