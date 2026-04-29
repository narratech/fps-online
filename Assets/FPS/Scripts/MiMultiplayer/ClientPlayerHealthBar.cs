using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

namespace Unity.FPS.UI
{
    /// <summary>
    /// Barra de vida local (UI) para el jugador.
    /// <para>
    /// En un setup de NGO, este HUD debería existir y actualizarse solo para el <see cref="NetworkBehaviour.IsOwner"/>.
    /// </para>
    /// <para>
    /// DEFECTUOSO (doble inicialización): inicializa en <see cref="Start"/> y vuelve a inicializar en
    /// <see cref="OnNetworkSpawn"/> (owner). Además, <see cref="Update"/> no comprueba:
    /// - IsOwner,
    /// - que <see cref="m_PlayerHealth"/> no sea null,
    /// - que <see cref="HealthFillImage"/> no sea null.
    /// Eso puede causar NRE o que instancias no-owner intenten actualizar UI.
    /// </para>
    /// </summary>
    public class ClientPlayerHealthBar : NetworkBehaviour
    {
        [Tooltip("Image component dispplaying current health")]
        /// <summary>Imagen cuyo fill representa el HP actual (0..1).</summary>
        public Image HealthFillImage;

        /// <summary>Referencia a la vida del jugador usado para computar el fill.</summary>
        Health m_PlayerHealth;

        /// <summary>
        /// Inicialización original del sample (no owner-gated).
        /// </summary>
        void Start()
        {
            PlayerCharacterController playerCharacterController =
                GameObject.FindFirstObjectByType<PlayerCharacterController>();
            DebugUtility.HandleErrorIfNullFindObject<PlayerCharacterController, PlayerHealthBar>(
                playerCharacterController, this);

            m_PlayerHealth = playerCharacterController.GetComponent<Health>();
            DebugUtility.HandleErrorIfNullGetComponent<Health, PlayerHealthBar>(m_PlayerHealth, this,
                playerCharacterController.gameObject);
        }

        public override void OnNetworkSpawn()
        {


            base.OnNetworkSpawn();

            if (IsOwner)
            {
                // En multijugador, esta ruta debería ser la principal para el player local.
                PlayerCharacterController playerCharacterController =
                 GameObject.FindFirstObjectByType<PlayerCharacterController>();
                DebugUtility.HandleErrorIfNullFindObject<PlayerCharacterController, PlayerHealthBar>(
                    playerCharacterController, this);

                m_PlayerHealth = playerCharacterController.GetComponent<Health>();
                DebugUtility.HandleErrorIfNullGetComponent<Health, PlayerHealthBar>(m_PlayerHealth, this,
                    playerCharacterController.gameObject);

            }


        }



        void Update()
        {
            // update health bar value
            HealthFillImage.fillAmount = m_PlayerHealth.CurrentHealth / m_PlayerHealth.MaxHealth;
        }
    }
}
