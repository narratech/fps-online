using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Unity.FPS.UI
{
    public class NotificationHUDManager : MonoBehaviour
    {
        [Tooltip("UI panel containing the layoutGroup for displaying notifications")]
        public RectTransform NotificationPanel;

        [Tooltip("Prefab for the notifications")]
        public GameObject NotificationPrefab;

        PlayerWeaponsManager m_PlayerWeaponsManager;
        Jetpack m_Jetpack;

        void Awake()
        {
            // En multiplayer/MPPM el jugador puede no existir aún en Awake.
            // Nos suscribimos más tarde cuando los componentes estén disponibles.
            EventManager.AddListener<ObjectiveUpdateEvent>(OnObjectiveUpdateEvent);
        }

        void Start()
        {
            TryBind();
        }

        void TryBind()
        {
            if (m_PlayerWeaponsManager == null)
            {
                m_PlayerWeaponsManager = FindFirstObjectByType<PlayerWeaponsManager>();
                if (m_PlayerWeaponsManager != null)
                    m_PlayerWeaponsManager.OnAddedWeapon += OnPickupWeapon;
            }

            if (m_Jetpack == null)
            {
                m_Jetpack = FindFirstObjectByType<Jetpack>();
                if (m_Jetpack != null)
                    m_Jetpack.OnUnlockJetpack += OnUnlockJetpack;
            }
        }

        void OnObjectiveUpdateEvent(ObjectiveUpdateEvent evt)
        {
            if (!string.IsNullOrEmpty(evt.NotificationText))
                CreateNotification(evt.NotificationText);
        }

        void OnPickupWeapon(WeaponController weaponController, int index)
        {
            if (index != 0)
                CreateNotification("Picked up weapon : " + weaponController.WeaponName);
        }

        void OnUnlockJetpack(bool unlock)
        {
            CreateNotification("Jetpack unlocked");
        }

        public void CreateNotification(string text)
        {
            if (NotificationPrefab == null || NotificationPanel == null)
                return;

            GameObject notificationInstance = Instantiate(NotificationPrefab, NotificationPanel);
            notificationInstance.transform.SetSiblingIndex(0);

            NotificationToast toast = notificationInstance.GetComponent<NotificationToast>();
            if (toast)
            {
                toast.Initialize(text);
            }
        }

        void OnDestroy()
        {
            EventManager.RemoveListener<ObjectiveUpdateEvent>(OnObjectiveUpdateEvent);

            if (m_PlayerWeaponsManager != null)
                m_PlayerWeaponsManager.OnAddedWeapon -= OnPickupWeapon;
            if (m_Jetpack != null)
                m_Jetpack.OnUnlockJetpack -= OnUnlockJetpack;
        }
    }
}