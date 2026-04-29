using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.FPS.UI;
using UnityEngine.ProBuilder; // Cambiado de using UnityEditor.ProBuilder;
using UnityEngine.InputSystem;

namespace Unity.FPS.UI
{
    /// <summary>
    /// Gestión del crosshair del jugador local (Owner) en función del arma activa y si está apuntando a un enemigo.
    /// <para>
    /// Este script proviene del sample pero se ha adaptado a NGO heredando de <see cref="NetworkBehaviour"/>.
    /// </para>
    /// <para>
    /// DEFECTUOSO (doble suscripción): se inicializa y se suscribe en <see cref="Start"/> y de nuevo en
    /// <see cref="OnNetworkSpawn"/> para owner. Esto puede duplicar handlers de <see cref="PlayerWeaponsManager.OnSwitchedToWeapon"/>
    /// y provocar llamadas repetidas. Idealmente se usaría solo OnNetworkSpawn (owner-only) y se limpiaría en OnDestroy/OnNetworkDespawn.
    /// </para>
    /// </summary>
    public class ClientCrosshairManager : NetworkBehaviour
    {
        /// <summary>Imagen UI del crosshair.</summary>
        public Image CrosshairImage;
        /// <summary>Sprite fallback cuando no hay arma (o arma sin crosshair definido).</summary>
        public Sprite NullCrosshairSprite;
        /// <summary>Suavizado de interpolación (color/tamaño) del crosshair.</summary>
        public float CrosshairUpdateshrpness = 5f;

        /// <summary>Gestor de armas del jugador local.</summary>
        PlayerWeaponsManager m_WeaponsManager;
        /// <summary>Estado previo para detectar cambios en "apuntando a enemigo".</summary>
        bool m_WasPointingAtEnemy;
        /// <summary>RectTransform del crosshair para animar el tamaño.</summary>
        RectTransform m_CrosshairRectTransform;
        /// <summary>Crosshair por defecto (arma activa).</summary>
        CrosshairData m_CrosshairDataDefault;
        /// <summary>Crosshair cuando el arma detecta enemigo en la mira.</summary>
        CrosshairData m_CrosshairDataTarget;
        /// <summary>Estado visual actual (interpolado hacia default/target).</summary>
        CrosshairData m_CurrentCrosshair;

        /// <summary>
        /// Inicialización original del sample.
        /// <para>
        /// DEFECTUOSO en NGO: corre también en no-owner y duplica la inicialización de OnNetworkSpawn.
        /// </para>
        /// </summary>
        void Start()
        {
            m_WeaponsManager = FindFirstObjectByType<PlayerWeaponsManager>();
            DebugUtility.HandleErrorIfNullFindObject<PlayerWeaponsManager, CrosshairManager>(m_WeaponsManager, this);

            OnWeaponChanged(m_WeaponsManager.GetActiveWeapon());

            m_WeaponsManager.OnSwitchedToWeapon += OnWeaponChanged;
        }


        public override void OnNetworkSpawn()
        {


            base.OnNetworkSpawn();

            if (IsOwner)
            {
                // Inicialización owner-only (preferible en multijugador).
                m_WeaponsManager = FindFirstObjectByType<PlayerWeaponsManager>();
                DebugUtility.HandleErrorIfNullFindObject<PlayerWeaponsManager, CrosshairManager>(m_WeaponsManager, this);

                OnWeaponChanged(m_WeaponsManager.GetActiveWeapon());

                m_WeaponsManager.OnSwitchedToWeapon += OnWeaponChanged;
            }


        }





        void Update()
        {
            // Nota: no se comprueba IsOwner. Si Start inicializa referencias en no-owner, también ejecutará.
            // Esto está relacionado con la duplicación Start/OnNetworkSpawn marcada como DEFECTUOSO.
            UpdateCrosshairPointingAtEnemy(false);
            m_WasPointingAtEnemy = m_WeaponsManager.IsPointingAtEnemy;
        }

        void UpdateCrosshairPointingAtEnemy(bool force)
        {
            // Cambia instantáneamente sprite/tamaño al detectar transición y luego interpola color/tamaño.
            if (m_CrosshairDataDefault.CrosshairSprite == null)
                return;

            if ((force || !m_WasPointingAtEnemy) && m_WeaponsManager.IsPointingAtEnemy)
            {
                m_CurrentCrosshair = m_CrosshairDataTarget;
                CrosshairImage.sprite = m_CurrentCrosshair.CrosshairSprite;
                m_CrosshairRectTransform.sizeDelta = m_CurrentCrosshair.CrosshairSize * Vector2.one;
            }
            else if ((force || m_WasPointingAtEnemy) && !m_WeaponsManager.IsPointingAtEnemy)
            {
                m_CurrentCrosshair = m_CrosshairDataDefault;
                CrosshairImage.sprite = m_CurrentCrosshair.CrosshairSprite;
                m_CrosshairRectTransform.sizeDelta = m_CurrentCrosshair.CrosshairSize * Vector2.one;
            }

            CrosshairImage.color = Color.Lerp(CrosshairImage.color, m_CurrentCrosshair.CrosshairColor,
                Time.deltaTime * CrosshairUpdateshrpness);

            m_CrosshairRectTransform.sizeDelta = Mathf.Lerp(m_CrosshairRectTransform.sizeDelta.x,
                m_CurrentCrosshair.CrosshairSize,
                Time.deltaTime * CrosshairUpdateshrpness) * Vector2.one;
        }

        void OnWeaponChanged(WeaponController newWeapon)
        {
            // Actualiza datos de crosshair al cambiar de arma.
            if (newWeapon)
            {
                CrosshairImage.enabled = true;
                m_CrosshairDataDefault = newWeapon.CrosshairDataDefault;
                m_CrosshairDataTarget = newWeapon.CrosshairDataTargetInSight;
                m_CrosshairRectTransform = CrosshairImage.GetComponent<RectTransform>();
                DebugUtility.HandleErrorIfNullGetComponent<RectTransform, CrosshairManager>(m_CrosshairRectTransform,
                    this, CrosshairImage.gameObject);
            }
            else
            {
                if (NullCrosshairSprite)
                {
                    CrosshairImage.sprite = NullCrosshairSprite;
                }
                else
                {
                    CrosshairImage.enabled = false;
                }
            }

            UpdateCrosshairPointingAtEnemy(true);
        }
    }
}
