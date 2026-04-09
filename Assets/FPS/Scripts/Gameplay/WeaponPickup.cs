using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    public class WeaponPickup : Pickup
    {
        [Tooltip("The prefab for the weapon that will be added to the player on pickup")]
        public WeaponController WeaponPrefab;

        protected override void Start()
        {
            base.Start();

            // Set all children layers to default (to prefent seeing weapons through meshes)
            foreach (Transform t in GetComponentsInChildren<Transform>())
            {
                if (t != transform)
                    t.gameObject.layer = 0;
            }
        }

        protected override void OnPicked(PlayerCharacterController byPlayer)
        {
            // --- EL TRUCO WRAPPER ---
            Component networkSync = GetComponent("NetworkPickupSync");
            if (networkSync != null)
            {
                // Si el objeto tiene nuestro script de red, le pasamos el recado y él decide
                networkSync.SendMessage("OnNetworkPickupRequested", byPlayer, SendMessageOptions.DontRequireReceiver);
            }
            else
            {
                // Si jugamos offline (sin script de red), damos el arma y destruimos el objeto localmente
                bool granted = GrantWeapon(byPlayer);
                if (granted)
                    GetComponent<LocalWorldPickupRespawn>()?.TryScheduleRespawnAtCurrentTransform();
                Destroy(gameObject);
            }
        }

        // --- FUNCIÓN PÚBLICA (Para que la llame el script de red) ---
        public bool GrantWeapon(PlayerCharacterController byPlayer)
        {
            PlayerWeaponsManager playerWeaponsManager = byPlayer.GetComponent<PlayerWeaponsManager>();
            if (!playerWeaponsManager)
                return false;

            if (playerWeaponsManager.HasWeapon(WeaponPrefab))
                return false;

            var prefabControllers = WeaponPrefab != null
                ? WeaponPrefab.GetComponentsInChildren<WeaponController>(true)
                : null;

            bool pickedAny = false;
            if (prefabControllers != null && prefabControllers.Length > 1)
            {
                foreach (var wc in prefabControllers)
                {
                    if (wc != null)
                        pickedAny |= playerWeaponsManager.AddWeapon(wc);
                }
            }
            else
            {
                pickedAny = playerWeaponsManager.AddWeapon(WeaponPrefab);
            }

            if (pickedAny)
            {
                if (playerWeaponsManager.GetActiveWeapon() == null)
                    playerWeaponsManager.SwitchWeapon(true);

                PlayPickupFeedback();
            }

            return pickedAny;
        }
    }
}