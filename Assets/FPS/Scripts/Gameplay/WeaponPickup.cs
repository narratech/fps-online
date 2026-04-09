using System.Reflection;
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
            // Solo usar red si el NetworkObject está spawneado; si no (p. ej. respawn local), flujo offline.
            var networkSync = GetComponent("NetworkPickupSync");
            if (networkSync != null && IsNetworkObjectSpawned(gameObject))
            {
                networkSync.SendMessage("OnNetworkPickupRequested", byPlayer, SendMessageOptions.DontRequireReceiver);
            }
            else
            {
                bool granted = GrantWeapon(byPlayer);
                if (granted)
                    LocalWorldPickupRespawn.ScheduleLocalRespawnFor(gameObject);
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

        static bool IsNetworkObjectSpawned(GameObject go)
        {
            var t = System.Type.GetType("Unity.Netcode.NetworkObject, Unity.Netcode.Runtime");
            if (t == null) return false;
            var comp = go.GetComponent(t);
            if (comp == null) return false;
            var p = t.GetProperty("IsSpawned", BindingFlags.Public | BindingFlags.Instance);
            return p != null && (bool)p.GetValue(comp);
        }
    }
}