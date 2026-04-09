using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using Unity.FPS.Gameplay;
using Unity.FPS.Game;
using UnityEngine.SceneManagement; // ¡NUEVO! Necesario para saber cuándo carga la escena
using TMPro;
using System.Text;
using UnityEngine.UI;
using System;
using UnityEngine.AI;

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

    [Header("UCM_Bot autopilot")]
    [Tooltip("Si el jugador local es UCM_Bot, desactiva control manual y lo mueve solo por NavMesh.")]
    [SerializeField] bool enableUcmBotAutopilot = true;

    [SerializeField] float ucmWanderRadius = 25f;
    [SerializeField] float ucmRepathIntervalSeconds = 1.25f;
    [SerializeField] float ucmStoppingDistance = 1.5f;

    [Header("Scoreboard HUD")]
    [SerializeField] private float scoreboardRefreshInterval = 0.25f;
    TextMeshProUGUI m_ScoreboardText;
    float m_NextScoreboardRefresh;
    readonly StringBuilder m_Sb = new StringBuilder(512);
    AudioListener m_TemporaryAudioListener;

    static TMP_FontAsset s_CachedRobotoHudFont;
    NavMeshAgent m_UcmAgent;
    bool m_IsUcmAutopilot;
    float m_NextUcmRepathTime;

    /// <summary>Misma fuente que Pause/Options en GameHUD (Roboto-Black SDF).</summary>
    static TMP_FontAsset ResolveGameHudFont()
    {
        if (s_CachedRobotoHudFont != null) return s_CachedRobotoHudFont;
        var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < fonts.Length; i++)
        {
            var f = fonts[i];
            if (f != null && string.Equals(f.name, "Roboto-Black SDF", StringComparison.Ordinal))
            {
                s_CachedRobotoHudFont = f;
                break;
            }
        }

        return s_CachedRobotoHudFont;
    }

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
            EnsureTemporaryAudioListener();

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
            if (m_TemporaryAudioListener != null)
            {
                Destroy(m_TemporaryAudioListener.gameObject);
                m_TemporaryAudioListener = null;
            }
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
        // No hacemos spawn inicial aquí: el servidor posiciona en un RespawnPoint aleatorio.
        // Solo nos aseguramos de que el CharacterController esté apagado antes de moverlo (si hiciera falta).
        if (m_characterController != null) m_characterController.enabled = false;

        // ¡Encendemos todo en el mapa correcto!
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

        // Aseguramos que haya un AudioListener activo (en MPPM puede aparecer el warning si aún no hay cámara activa)
        EnsureAudioListener();

        EnsureScoreboardUI();

        TryEnableUcmAutopilotIfNeeded();
    }

    bool IsUcmBotInstance()
    {
        // En runtime suele venir "UCM_Bot(Clone)"
        return gameObject != null && gameObject.name.StartsWith("UCM_Bot", StringComparison.Ordinal);
    }

    void TryEnableUcmAutopilotIfNeeded()
    {
        if (!IsOwner) return;
        if (!enableUcmBotAutopilot) return;
        if (!IsUcmBotInstance()) return;

        m_IsUcmAutopilot = true;

        // Desactivamos control manual (pero dejamos cámara/vida/armas para ver lo que hace).
        if (m_playerInputHandler != null) m_playerInputHandler.enabled = false;
        if (m_playerCharacterController != null) m_playerCharacterController.enabled = false;
        if (m_jetpack != null) m_jetpack.enabled = false;
        if (m_playerinput != null) m_playerinput.enabled = false;
        if (m_characterController != null) m_characterController.enabled = false;

        if (m_UcmAgent == null)
            m_UcmAgent = GetComponent<NavMeshAgent>();
        if (m_UcmAgent == null)
            m_UcmAgent = gameObject.AddComponent<NavMeshAgent>();

        m_UcmAgent.enabled = true;
        m_UcmAgent.stoppingDistance = Mathf.Max(0.25f, ucmStoppingDistance);
        m_UcmAgent.autoBraking = true;

        // Evita que el agente trate de “pelearse” con componentes desactivados.
        m_UcmAgent.updatePosition = true;
        m_UcmAgent.updateRotation = true;

        m_NextUcmRepathTime = 0f;
    }

    void UpdateUcmAutopilot()
    {
        if (!m_IsUcmAutopilot) return;
        if (!IsOwner) return;
        if (m_UcmAgent == null || !m_UcmAgent.enabled) return;
        if (!m_UcmAgent.isOnNavMesh) return;

        if (Time.time >= m_NextUcmRepathTime)
        {
            m_NextUcmRepathTime = Time.time + Mathf.Max(0.1f, ucmRepathIntervalSeconds);

            bool needsNewTarget = !m_UcmAgent.hasPath || m_UcmAgent.pathPending ||
                                  m_UcmAgent.remainingDistance <= (m_UcmAgent.stoppingDistance + 0.35f);

            if (needsNewTarget)
            {
                if (TryPickRandomNavMeshPoint(transform.position, Mathf.Max(2f, ucmWanderRadius), out var dest))
                    m_UcmAgent.SetDestination(dest);
            }
        }
    }

    static bool TryPickRandomNavMeshPoint(Vector3 origin, float radius, out Vector3 result)
    {
        for (int i = 0; i < 20; i++)
        {
            var rnd = UnityEngine.Random.insideUnitSphere * radius;
            var candidate = origin + rnd;
            if (NavMesh.SamplePosition(candidate, out var hit, 2.0f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }

        result = origin;
        return false;
    }

    void EnsureAudioListener()
    {
        if (!IsOwner) return;
        if (m_camera == null) return;

        var listener = m_camera.GetComponent<AudioListener>();
        if (listener != null)
        {
            listener.enabled = true;
            if (m_TemporaryAudioListener != null)
            {
                Destroy(m_TemporaryAudioListener.gameObject);
                m_TemporaryAudioListener = null;
            }
        }
    }

    void EnsureTemporaryAudioListener()
    {
        if (!IsOwner) return;
        if (FindFirstObjectByType<AudioListener>() != null) return;

        var go = new GameObject("TemporaryAudioListener");
        m_TemporaryAudioListener = go.AddComponent<AudioListener>();
    }

    void Update()
    {
        if (!IsOwner) return;

        // Fix suave: si por algún motivo no hay AudioListener activo (muerte/respawn/cámara toggle en MPPM),
        // creamos uno temporal y lo retiramos cuando vuelva el de la cámara.
        if (Time.frameCount % 20 == 0)
        {
            if (FindFirstObjectByType<AudioListener>() == null)
                EnsureTemporaryAudioListener();
            else
                EnsureAudioListener();
        }

        UpdateUcmAutopilot();

        if (Time.unscaledTime < m_NextScoreboardRefresh) return;
        m_NextScoreboardRefresh = Time.unscaledTime + scoreboardRefreshInterval;

        if (!EsEscenaJugable(SceneManager.GetActiveScene().name))
            return;

        EnsureScoreboardUI();
        if (m_ScoreboardText == null) return;

        var allTags = FindObjectsByType<PlayerNameTag>(FindObjectsSortMode.None);

        m_Sb.Clear();
        for (int i = 0; i < allTags.Length; i++)
        {
            var t = allTags[i];
            string name = t.NetworkedName.Value.ToString();
            if (string.IsNullOrWhiteSpace(name))
                name = $"Player {t.OwnerClientId}";

            m_Sb.Append(name);
            m_Sb.Append("  Kills: ");
            m_Sb.Append(t.Kills.Value);
            m_Sb.Append("  Deaths: ");
            m_Sb.Append(t.Deaths.Value);
            m_Sb.AppendLine();
        }

        m_ScoreboardText.text = m_Sb.ToString();
    }

    void EnsureScoreboardUI()
    {
        if (m_ScoreboardText != null) return;
        if (!IsOwner) return;

        // El Canvas que traen los prefabs está en World Space (m_RenderMode = 2),
        // así que para un HUD en pantalla creamos nuestro propio Canvas Overlay local.
        var hudCanvasGo = new GameObject("ScoreboardCanvas");
        var canvas = hudCanvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 999;

        var scaler = hudCanvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        hudCanvasGo.AddComponent<GraphicRaycaster>();

        var go = new GameObject("ScoreboardText", typeof(RectTransform));
        go.transform.SetParent(hudCanvasGo.transform, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-16f, -16f);
        rt.sizeDelta = new Vector2(640f, 420f);

        m_ScoreboardText = go.AddComponent<TextMeshProUGUI>();
        var hudFont = ResolveGameHudFont();
        if (hudFont != null)
            m_ScoreboardText.font = hudFont;
        else if (TMP_Settings.defaultFontAsset != null)
            m_ScoreboardText.font = TMP_Settings.defaultFontAsset;

        m_ScoreboardText.fontSize = 18;
        m_ScoreboardText.fontStyle = FontStyles.Normal;
        m_ScoreboardText.enableWordWrapping = true;
        m_ScoreboardText.alignment = TextAlignmentOptions.TopRight;
        m_ScoreboardText.text = "";
    }
}