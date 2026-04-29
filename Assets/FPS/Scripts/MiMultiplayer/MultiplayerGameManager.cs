using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.FPS.Game
{
    /// <summary>
    /// Gestor de fin de partida (win/lose) adaptado para multijugador con NGO.
    /// <para>
    /// En modo offline: carga escenas con <see cref="SceneManager.LoadScene"/>.
    /// En modo multijugador:
    /// - Solo el servidor/host inicia la transición.
    /// - La escena se cambia usando <see cref="NetworkSceneManager.LoadScene"/> para arrastrar a todos los clientes.
    /// - Los clientes puros no hacen nada en la carga final (esperan al servidor).
    /// </para>
    /// </summary>
    public class MultiplayerGameManager : MonoBehaviour
    {
        [Header("Parameters")]
        [Tooltip("Duration of the fade-to-black at the end of the game")]
        /// <summary>Duración base del fundido a negro al terminar.</summary>
        public float EndSceneLoadDelay = 3f;

        [Tooltip("The canvas group of the fade-to-black screen")]
        /// <summary>CanvasGroup que se usa para el fade.</summary>
        public CanvasGroup EndGameFadeCanvasGroup;

        [Header("Win")]
        [Tooltip("This string has to be the name of the scene you want to load when winning")]
        /// <summary>Escena a cargar cuando se gana.</summary>
        public string WinSceneName = "WinScene";

        [Tooltip("Duration of delay before the fade-to-black, if winning")]
        /// <summary>Delay adicional antes del fade cuando se gana.</summary>
        public float DelayBeforeFadeToBlack = 4f;

        [Tooltip("Win game message")]
        /// <summary>Mensaje de victoria (UI).</summary>
        public string WinGameMessage;
        [Tooltip("Duration of delay before the win message")]
        /// <summary>Delay antes de mostrar el mensaje de victoria.</summary>
        public float DelayBeforeWinMessage = 2f;

        [Tooltip("Sound played on win")] public AudioClip VictorySound;

        [Header("Lose")]
        [Tooltip("This string has to be the name of the scene you want to load when losing")]
        /// <summary>Escena a cargar cuando se pierde.</summary>
        public string LoseSceneName = "LoseScene";

        /// <summary>Indica si ya se disparó la secuencia de fin de juego.</summary>
        public bool GameIsEnding { get; private set; }

        /// <summary>Timestamp cuando debe cargarse la escena final.</summary>
        float m_TimeLoadEndGameScene;
        /// <summary>Nombre de la escena final a cargar (win/lose).</summary>
        string m_SceneToLoad;

        /// <summary>Suscripción al evento de muerte del jugador.</summary>
        void Awake()
        {
            EventManager.AddListener<PlayerDeathEvent>(OnPlayerDeath);
        }

        void Start()
        {
            AudioUtility.SetMasterVolume(1);
        }

        void Update()
        {
            if (GameIsEnding)
            {
                // Interpola alpha y volumen durante el fade.
                float timeRatio = 1 - (m_TimeLoadEndGameScene - Time.time) / EndSceneLoadDelay;
                EndGameFadeCanvasGroup.alpha = timeRatio;

                AudioUtility.SetMasterVolume(1 - timeRatio);

                // See if it's time to load the end scene (after the delay)
                if (Time.time >= m_TimeLoadEndGameScene)
                {
                    GameIsEnding = false;

                    // --- LÓGICA DE CARGA DE ESCENAS MULTIJUGADOR ---

                    // Si estamos conectados a una red y somos el Host (Servidor)...
                    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                    {
                        // Usamos Netcode para llevar a todos los clientes a la nueva escena
                        NetworkManager.Singleton.SceneManager.LoadScene(m_SceneToLoad, LoadSceneMode.Single);
                    }
                    else if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
                    {
                        // Si no hay red (modo 1 jugador), cargamos normal
                        SceneManager.LoadScene(m_SceneToLoad);
                    }
                    // Si somos simplemente un cliente conectado (NetworkManager.Singleton.IsClient == true),
                    // no hacemos NADA aquí. Esperamos a que el servidor nos lleve automáticamente.
                }
            }
        }

        void OnPlayerDeath(PlayerDeathEvent evt) => EndGame(false);

        /// <summary>
        /// Inicia la secuencia de fin de partida (solo host/servidor o offline).
        /// </summary>
        void EndGame(bool win)
        {
            // Solo iniciamos la secuencia de fin de juego si somos el servidor (Host) o estamos jugando offline.
            // Si somos un cliente, ignoramos esto porque el servidor ya se encargará de cambiar de escena por nosotros.
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                return; // Los clientes puros no inician transiciones de escena
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            GameIsEnding = true;
            EndGameFadeCanvasGroup.gameObject.SetActive(true);

            if (win)
            {
                m_SceneToLoad = WinSceneName;
                m_TimeLoadEndGameScene = Time.time + EndSceneLoadDelay + DelayBeforeFadeToBlack;

                var audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.clip = VictorySound;
                audioSource.playOnAwake = false;
                audioSource.outputAudioMixerGroup = AudioUtility.GetAudioGroup(AudioUtility.AudioGroups.HUDVictory);
                audioSource.PlayScheduled(AudioSettings.dspTime + DelayBeforeWinMessage);

                DisplayMessageEvent displayMessage = Events.DisplayMessageEvent;
                displayMessage.Message = WinGameMessage;
                displayMessage.DelayBeforeDisplay = DelayBeforeWinMessage;
                EventManager.Broadcast(displayMessage);
            }
            else
            {
                m_SceneToLoad = LoseSceneName;
                m_TimeLoadEndGameScene = Time.time + EndSceneLoadDelay;
            }
        }

        void OnDestroy()
        {
            // Limpieza de evento.
            EventManager.RemoveListener<PlayerDeathEvent>(OnPlayerDeath);
        }
    }
}