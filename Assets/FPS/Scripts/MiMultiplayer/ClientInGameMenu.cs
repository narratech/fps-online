using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    // Cambiado a MonoBehaviour normal, la interfaz UI no necesita ser NetworkBehaviour
    /// <summary>
    /// Menú in-game (pausa/opciones) adaptado a multijugador.
    /// <para>
    /// En multijugador NO se usa <c>Time.timeScale</c> porque afectaría a todos o desincronizaría simulación.
    /// En su lugar, el menú solo actúa localmente: cursor, volumen y toggles de opciones.
    /// </para>
    /// <para>
    /// Estrategia de ownership:
    /// - Este script es local/UI y no hereda de NetworkBehaviour.
    /// - Aun así, busca al player local (owner) por <see cref="NetworkObject.IsOwner"/> para operar sobre su input/health.
    /// </para>
    /// </summary>
    public class ClientInGameMenu : MonoBehaviour
    {
        [Tooltip("Root GameObject of the menu used to toggle its activation")]
        /// <summary>Root del menú (se activa/desactiva).</summary>
        public GameObject MenuRoot;

        [Tooltip("Master volume when menu is open")]
        [Range(0.001f, 1f)]
        /// <summary>Volumen master cuando el menú está abierto.</summary>
        public float VolumeWhenMenuOpen = 0.5f;

        [Tooltip("Slider component for look sensitivity")]
        /// <summary>Slider de sensibilidad del ratón (se aplica a <see cref="PlayerInputHandler.LookSensitivity"/>).</summary>
        public Slider LookSensitivitySlider;

        [Tooltip("Toggle component for shadows")]
        /// <summary>Toggle de sombras (modifica <see cref="QualitySettings.shadows"/> localmente).</summary>
        public Toggle ShadowsToggle;

        [Tooltip("Toggle component for invincibility")]
        /// <summary>Toggle de invencibilidad (modifica <see cref="Health.Invincible"/> del player local).</summary>
        public Toggle InvincibilityToggle;

        [Tooltip("Toggle component for framerate display")]
        /// <summary>Toggle para mostrar/ocultar contador de FPS.</summary>
        public Toggle FramerateToggle;

        [Tooltip("GameObject for the controls")]
        /// <summary>Panel de controles (imagen) que se puede mostrar desde el menú.</summary>
        public GameObject ControlImage;

        /// <summary>Input handler del player local.</summary>
        PlayerInputHandler m_PlayerInputsHandler;
        /// <summary>Salud del player local (para invencibilidad).</summary>
        Health m_PlayerHealth;
        /// <summary>Contador de FPS (UI).</summary>
        FramerateCounter m_FramerateCounter;

        private InputAction m_SubmitAction;
        private InputAction m_CancelAction;
        private InputAction m_NavigateAction;
        private InputAction m_MenuAction;

        /// <summary>Evita inicializar más de una vez (se setea al resolver el player owner).</summary>
        private bool isInitialized = false;

        void Awake()
        {
            // Inicialización local de acciones UI.
            MenuRoot.SetActive(false);

            // Preparamos los controles, pero no buscamos al jugador aún
            m_SubmitAction = InputSystem.actions.FindAction("UI/Submit");
            m_CancelAction = InputSystem.actions.FindAction("UI/Cancel");
            m_NavigateAction = InputSystem.actions.FindAction("UI/Navigate");
            m_MenuAction = InputSystem.actions.FindAction("UI/Menu");

            if (m_SubmitAction != null) m_SubmitAction.Enable();
            if (m_CancelAction != null) m_CancelAction.Enable();
            if (m_NavigateAction != null) m_NavigateAction.Enable();
            if (m_MenuAction != null) m_MenuAction.Enable();
        }

        void Update()
        {
            // --- 1. ESPERAMOS AL JUGADOR LOCAL ---
            if (!isInitialized)
            {
                PlayerInputHandler[] players = FindObjectsByType<PlayerInputHandler>(FindObjectsSortMode.None);
                foreach (var player in players)
                {
                    NetworkObject netObj = player.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsOwner)
                    {
                        // ¡Encontramos a nuestro jugador local!
                        InitializeMenu(player);
                        break;
                    }
                }
                return; // Si aún no se ha inicializado, no ejecutamos el resto del Update
            }

            // --- 2. LÓGICA DEL MENÚ ---

            // Lock cursor when clicking outside of menu
            if (!MenuRoot.activeSelf && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if ((m_MenuAction != null && m_MenuAction.WasPressedThisFrame()) ||
                (MenuRoot.activeSelf && m_CancelAction != null && m_CancelAction.WasPressedThisFrame()))
            {
                if (ControlImage != null && ControlImage.activeSelf)
                {
                    ControlImage.SetActive(false);
                    return;
                }

                SetPauseMenuActivation(!MenuRoot.activeSelf);
            }

            if (m_NavigateAction != null && m_NavigateAction.ReadValue<Vector2>().y != 0)
            {
                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                    if (LookSensitivitySlider != null) LookSensitivitySlider.Select();
                }
            }
        }

        void InitializeMenu(PlayerInputHandler localPlayer)
        {
            // Binding a referencias del player local y wiring de UI.
            m_PlayerInputsHandler = localPlayer;
            m_PlayerHealth = m_PlayerInputsHandler.GetComponent<Health>();
            m_FramerateCounter = FindFirstObjectByType<FramerateCounter>();

            if (LookSensitivitySlider != null)
            {
                LookSensitivitySlider.value = m_PlayerInputsHandler.LookSensitivity;
                LookSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
            }

            if (ShadowsToggle != null)
            {
                ShadowsToggle.isOn = QualitySettings.shadows != ShadowQuality.Disable;
                ShadowsToggle.onValueChanged.AddListener(OnShadowsChanged);
            }

            if (InvincibilityToggle != null && m_PlayerHealth != null)
            {
                InvincibilityToggle.isOn = m_PlayerHealth.Invincible;
                InvincibilityToggle.onValueChanged.AddListener(OnInvincibilityChanged);
            }

            if (FramerateToggle != null && m_FramerateCounter != null)
            {
                FramerateToggle.isOn = m_FramerateCounter.UIText.gameObject.activeSelf;
                FramerateToggle.onValueChanged.AddListener(OnFramerateCounterChanged);
            }

            isInitialized = true;
        }

        public void ClosePauseMenu()
        {
            SetPauseMenuActivation(false);
        }

        void SetPauseMenuActivation(bool active)
        {
            // Activación local: cursor + volumen. No altera timeScale (multiplayer).
            MenuRoot.SetActive(active);

            if (MenuRoot.activeSelf)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                // ¡ELIMINADO Time.timeScale = 0f; PORQUE ESTO ES MULTIJUGADOR!

                AudioUtility.SetMasterVolume(VolumeWhenMenuOpen);
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                // ¡ELIMINADO Time.timeScale = 1f; PORQUE ESTO ES MULTIJUGADOR!

                AudioUtility.SetMasterVolume(1);
            }
        }

        void OnMouseSensitivityChanged(float newValue)
        {
            if (m_PlayerInputsHandler != null) m_PlayerInputsHandler.LookSensitivity = newValue;
        }

        void OnShadowsChanged(bool newValue)
        {
            QualitySettings.shadows = newValue ? ShadowQuality.All : ShadowQuality.Disable;
        }

        void OnInvincibilityChanged(bool newValue)
        {
            if (m_PlayerHealth != null) m_PlayerHealth.Invincible = newValue;
        }

        void OnFramerateCounterChanged(bool newValue)
        {
            if (m_FramerateCounter != null && m_FramerateCounter.UIText != null)
                m_FramerateCounter.UIText.gameObject.SetActive(newValue);
        }

        public void OnShowControlButtonClicked(bool show)
        {
            if (ControlImage != null) ControlImage.SetActive(show);
        }
    }
}