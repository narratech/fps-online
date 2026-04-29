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

/// <summary>
/// Componente de activación/hibernación del jugador local en un flujo multiescena con Netcode (NGO).
/// <para>
/// Responsabilidades actuales:
/// - Mantener DESACTIVADOS (en <see cref="Awake"/>) cámara, input y controladores mientras se está en menús para
///   evitar que el player caiga/actúe en escenas no jugables.
/// - Activar esos componentes cuando se carga una escena "jugable" (definida por <see cref="gameplaySceneNames"/>).
/// - Asegurar que siempre exista algún <see cref="AudioListener"/> activo en escenas donde el player aún no
///   ha activado su cámara (workaround contra warnings típicos en multijugador).
/// - Crear un HUD de marcador local (texto TMP) y refrescarlo periódicamente.
/// </para>
/// <para>
/// <b>DEFECTUOSO (naming):</b> el nombre <c>NewMonoBehaviourScript</c> no describe intención ni dominio.
/// Renombrar el archivo/clase mejoraría mantenibilidad, pero este cambio se marca solo como observación
/// porque aquí se pidió no tocar código.
/// </para>
/// <para>
/// <b>PRESCINDIBLE (duplicación):</b> el marcador también existe como `ClientScoreboardHUD` + `PlayerStats`.
/// Mantener dos implementaciones complica depuración y puede mostrar datos distintos según cuál esté activo.
/// Idealmente, se unifica en una sola ruta (o se documenta cuál es la oficial).
/// </para>
/// <para>
/// <b>Red:</b> la activación se limita a <see cref="NetworkBehaviour.IsOwner"/> (solo el jugador local).
/// No mueve/spawnea el player: la posición inicial/respawn debe ser autoritativa del servidor.
/// </para>
/// </summary>
public class NewMonoBehaviourScript : NetworkBehaviour
{
    /// <summary>
    /// Puente al input del FPS Sample (lee acciones y las traduce a variables del controlador).
    /// Se desactiva en menú para que no procese eventos en escenas no jugables.
    /// </summary>
    [SerializeField] private PlayerInputHandler m_playerInputHandler;
    /// <summary>
    /// Controlador de locomoción del personaje (lógica de movimiento/estado).
    /// En menús se desactiva para evitar simular física fuera del gameplay.
    /// </summary>
    [SerializeField] private PlayerCharacterController m_playerCharacterController;
    /// <summary>
    /// Componente de salud. Se desactiva en menú para evitar recibir daño/emitir eventos en escenas de UI.
    /// </summary>
    [SerializeField] private Health m_Health;
    /// <summary>
    /// Gestor de armas del jugador. Se desactiva en menú para evitar inicializar armas y lógica de disparo.
    /// </summary>
    [SerializeField] private PlayerWeaponsManager m_WeaponsManager;
    /// <summary>
    /// Jetpack (si aplica). Se desactiva en menú por consistencia con la locomoción.
    /// </summary>
    [SerializeField] private Jetpack m_jetpack;
    /// <summary>
    /// <see cref="CharacterController"/> de Unity. Debe desactivarse antes de teletransportar/activar para evitar
    /// empujes/collisions inesperadas.
    /// </summary>
    [SerializeField] private CharacterController m_characterController;
    /// <summary>
    /// Actor del framework (registro en <c>ActorsManager</c>, tags de equipo, etc.). Se desactiva en menú.
    /// </summary>
    [SerializeField] private Actor m_actor;
    /// <summary>
    /// Daño/impactos (parte del pipeline de combate). Se desactiva en menú para no procesar colisiones/daño.
    /// </summary>
    [SerializeField] private Damageable m_damageable;
    /// <summary>
    /// Componente de Input System (<see cref="PlayerInput"/>). Se desactiva en menú para no consumir acciones.
    /// </summary>
    [SerializeField] private PlayerInput m_playerinput;
    /// <summary>
    /// Referencia a la cámara del jugador local (GameObject). Se activa solo en escenas jugables.
    /// </summary>
    [SerializeField] private GameObject m_camera;

    [Header("Gameplay Scenes")]
    [Tooltip("Escenas donde el jugador debe activarse (cámara, input, controller, etc.).")]
    /// <summary>
    /// Lista blanca de escenas donde el jugador "debe vivir" como personaje controlable.
    /// En cualquier otra escena se mantiene apagado para evitar efectos colaterales (caer, sonidos, etc.).
    /// </summary>
    [SerializeField] private string[] gameplaySceneNames = { "MainScene", "PrisonScene", "SecondaryScene" };

    [Header("Scoreboard HUD")]
    /// <summary>
    /// Periodo de refresco del texto del marcador (tiempo no escalado).
    /// </summary>
    [SerializeField] private float scoreboardRefreshInterval = 0.25f;
    /// <summary>
    /// Texto TMP que se crea perezosamente en un canvas overlay local (ver <see cref="EnsureScoreboardUI"/>).
    /// </summary>
    TextMeshProUGUI m_ScoreboardText;
    /// <summary>
    /// Próximo instante (en <see cref="Time.unscaledTime"/>) en el que se recomputa el marcador.
    /// </summary>
    float m_NextScoreboardRefresh;
    /// <summary>
    /// Buffer reutilizable para evitar GC al reconstruir el marcador.
    /// </summary>
    readonly StringBuilder m_Sb = new StringBuilder(512);
    /// <summary>
    /// AudioListener temporal "de emergencia" cuando aún no hay cámara activa.
    /// Se destruye cuando se detecta un listener real en <see cref="m_camera"/>.
    /// </summary>
    AudioListener m_TemporaryAudioListener;

    /// <summary>Cache global de la fuente Roboto usada por el HUD del sample.</summary>
    static TMP_FontAsset s_CachedRobotoHudFont;

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
        // Apagamos todo al nacer para que no caiga al vacío en el Menú.
        // Nota de red: este objeto existirá en TODOS los clientes; solo el Owner reactivará lo necesario.
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

            // 1. Nos suscribimos al evento "Cuando una escena termine de cargar".
            // Esto permite activar el player aunque el NetworkObject haya spawneado antes que la escena jugable.
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
        // Limpieza de memoria súper importante al destruir el objeto.
        // Si no se desuscribe, el callback podría quedar apuntando a una instancia destruida tras cambiar de escena.
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
        // Solo reaccionamos si la escena que acaba de cargar es jugable (lista blanca).
        if (EsEscenaJugable(scene.name))
        {
            UbicarYEncenderJugador();
        }
    }

    private bool EsEscenaJugable(string sceneName)
    {
        // Comparación exacta por nombre. Si cambiáis nombres de escena, actualizad gameplaySceneNames.
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
        // Nota: esto solo debe ejecutarse en el Owner (ver OnNetworkSpawn), porque habilita input/cámara.
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
    }

    void EnsureAudioListener()
    {
        // Garantía: en Unity solo debe existir un AudioListener activo (por escena) para evitar warnings
        // y mezcla de audio incorrecta. Aquí priorizamos el listener de la cámara del player local.
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
        // Workaround: durante transiciones, puede que no haya cámara activa todavía.
        // Creamos un listener temporal para evitar warnings y asegurar que el audio "tiene receptor".
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

        // El marcador se actualiza a un ritmo limitado para evitar coste en UI y búsquedas de objetos.
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
        // Crea un Canvas overlay local (solo Owner) para mostrar el scoreboard.
        // PRESCINDIBLE si se adopta `ClientScoreboardHUD` + `PlayerStats` como implementación única.
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