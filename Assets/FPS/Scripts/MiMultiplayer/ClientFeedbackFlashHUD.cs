using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine.ProBuilder; // Cambiado de using UnityEditor.ProBuilder;
using UnityEngine.InputSystem;


namespace Unity.FPS.UI
{

    /// <summary>
    /// Flash/vignette de feedback al recibir daño y al curarse, adaptado a multijugador.
    /// <para>
    /// Se pretende que solo el player local (Owner) muestre este HUD, ya que es feedback de primera persona.
    /// </para>
    /// <para>
    /// DEFECTUOSO (doble suscripción): este script se suscribe a eventos de salud en <see cref="Start"/> y
    /// también en <see cref="OnNetworkSpawn"/> cuando <see cref="NetworkBehaviour.IsOwner"/> es true.
    /// Eso puede provocar:
    /// - handlers duplicados (doble flash),
    /// - fugas si no se desuscribe.
    /// Lo correcto sería elegir UNA vía de inicialización (preferible OnNetworkSpawn en NGO) y
    /// desuscribirse en OnDestroy/OnNetworkDespawn.
    /// </para>
    /// </summary>
    public class ClientFeedbackFlashHUD : NetworkBehaviour
    {
        [Header("References")]
        [Tooltip("Image component of the flash")]
        /// <summary>Imagen usada para el flash (color cambia según daño/curación).</summary>
        public Image FlashImage;

        [Tooltip("CanvasGroup to fade the damage flash, used when recieving damage end healing")]
        /// <summary>CanvasGroup del flash (alpha animada por tiempo).</summary>
        public CanvasGroup FlashCanvasGroup;

        [Tooltip("CanvasGroup to fade the critical health vignette")]
        /// <summary>CanvasGroup de la viñeta de salud crítica.</summary>
        public CanvasGroup VignetteCanvasGroup;

        [Header("Damage")]
        [Tooltip("Color of the damage flash")]
        /// <summary>Color del flash al recibir daño.</summary>
        public Color DamageFlashColor;

        [Tooltip("Duration of the damage flash")]
        /// <summary>Duración del flash de daño.</summary>
        public float DamageFlashDuration;

        [Tooltip("Max alpha of the damage flash")]
        /// <summary>Alpha máxima del flash de daño.</summary>
        public float DamageFlashMaxAlpha = 1f;

        [Header("Critical health")]
        [Tooltip("Max alpha of the critical vignette")]
        /// <summary>Alpha máxima de la viñeta en salud crítica.</summary>
        public float CriticaHealthVignetteMaxAlpha = .8f;

        [Tooltip("Frequency at which the vignette will pulse when at critical health")]
        /// <summary>Frecuencia de pulsación de la viñeta (Hz aprox.).</summary>
        public float PulsatingVignetteFrequency = 4f;

        [Header("Heal")]
        [Tooltip("Color of the heal flash")]
        /// <summary>Color del flash al curarse.</summary>
        public Color HealFlashColor;

        [Tooltip("Duration of the heal flash")]
        /// <summary>Duración del flash de curación.</summary>
        public float HealFlashDuration;

        [Tooltip("Max alpha of the heal flash")]
        /// <summary>Alpha máxima del flash de curación.</summary>
        public float HealFlashMaxAlpha = 1f;

        /// <summary>Estado interno: si hay un flash activo en curso.</summary>
        bool m_FlashActive;
        /// <summary>Timestamp del inicio del último flash.</summary>
        float m_LastTimeFlashStarted = Mathf.NegativeInfinity;
        /// <summary>Vida del jugador local.</summary>
        Health m_PlayerHealth;
        /// <summary>Gestor de flujo (para saber si termina partida y evitar pulsación).</summary>
        GameFlowManager m_GameFlowManager;

        /// <summary>
        /// Inicialización original del sample.
        /// <para>
        /// DEFECTUOSO: en NGO esto corre tanto en owner como en no-owner y además duplica la lógica de OnNetworkSpawn.
        /// Se mantiene sin tocar por la restricción de "no cambiar código".
        /// </para>
        /// </summary>
        void Start()
        {
            // Subscribe to player damage events
            PlayerCharacterController playerCharacterController = FindFirstObjectByType<PlayerCharacterController>();
            DebugUtility.HandleErrorIfNullFindObject<PlayerCharacterController, FeedbackFlashHUD>(
                playerCharacterController, this);

            m_PlayerHealth = playerCharacterController.GetComponent<Health>();
            DebugUtility.HandleErrorIfNullGetComponent<Health, FeedbackFlashHUD>(m_PlayerHealth, this,
                playerCharacterController.gameObject);

            m_GameFlowManager = FindFirstObjectByType<GameFlowManager>();
            DebugUtility.HandleErrorIfNullFindObject<GameFlowManager, FeedbackFlashHUD>(m_GameFlowManager, this);

            m_PlayerHealth.OnDamaged += OnTakeDamage;
            m_PlayerHealth.OnHealed += OnHealed;
        }


        public override void OnNetworkSpawn()
        {


            base.OnNetworkSpawn();

            if (IsOwner)
            {
                // Inicialización owner-only (preferible en multijugador).
                // Subscribe to player damage events
                PlayerCharacterController playerCharacterController = FindFirstObjectByType<PlayerCharacterController>();
                DebugUtility.HandleErrorIfNullFindObject<PlayerCharacterController, FeedbackFlashHUD>(
                    playerCharacterController, this);

                m_PlayerHealth = playerCharacterController.GetComponent<Health>();
                DebugUtility.HandleErrorIfNullGetComponent<Health, FeedbackFlashHUD>(m_PlayerHealth, this,
                    playerCharacterController.gameObject);

                m_GameFlowManager = FindFirstObjectByType<GameFlowManager>();
                DebugUtility.HandleErrorIfNullFindObject<GameFlowManager, FeedbackFlashHUD>(m_GameFlowManager, this);

                m_PlayerHealth.OnDamaged += OnTakeDamage;
                m_PlayerHealth.OnHealed += OnHealed;

            }


        }




        void Update()
        {
            // Nota: no se comprueba IsOwner. En no-owner, si Start ya inicializó referencias, también ejecutará.
            // Esto es parte de la problemática de doble inicialización (ver cabecera).
            if (m_PlayerHealth.IsCritical())
            {
                VignetteCanvasGroup.gameObject.SetActive(true);
                float vignetteAlpha =
                    (1 - (m_PlayerHealth.CurrentHealth / m_PlayerHealth.MaxHealth /
                          m_PlayerHealth.CriticalHealthRatio)) * CriticaHealthVignetteMaxAlpha;

                if (m_GameFlowManager.GameIsEnding)
                    VignetteCanvasGroup.alpha = vignetteAlpha;
                else
                    VignetteCanvasGroup.alpha =
                        ((Mathf.Sin(Time.time * PulsatingVignetteFrequency) / 2) + 0.5f) * vignetteAlpha;
            }
            else
            {
                VignetteCanvasGroup.gameObject.SetActive(false);
            }


            if (m_FlashActive)
            {
                float normalizedTimeSinceDamage = (Time.time - m_LastTimeFlashStarted) / DamageFlashDuration;

                if (normalizedTimeSinceDamage < 1f)
                {
                    float flashAmount = DamageFlashMaxAlpha * (1f - normalizedTimeSinceDamage);
                    FlashCanvasGroup.alpha = flashAmount;
                }
                else
                {
                    FlashCanvasGroup.gameObject.SetActive(false);
                    m_FlashActive = false;
                }
            }
        }

        void ResetFlash()
        {
            // Resetea estado para iniciar un flash nuevo.
            m_LastTimeFlashStarted = Time.time;
            m_FlashActive = true;
            FlashCanvasGroup.alpha = 0f;
            FlashCanvasGroup.gameObject.SetActive(true);
        }

        void OnTakeDamage(float dmg, GameObject damageSource)
        {
            // Evento de daño: flash rojo (o DamageFlashColor configurado).
            ResetFlash();
            FlashImage.color = DamageFlashColor;
        }

        void OnHealed(float amount)
        {
            // Evento de curación: flash verde (o HealFlashColor configurado).
            ResetFlash();
            FlashImage.color = HealFlashColor;
        }
    }
}