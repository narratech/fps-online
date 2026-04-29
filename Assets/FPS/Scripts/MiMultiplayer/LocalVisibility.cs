using System.Globalization;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.FPS.Gameplay
{
    /// <summary>
    /// Controla la visibilidad local del cuerpo/brazos/armas 3ª persona según si el objeto pertenece al jugador local.
    /// <para>
    /// Propósito: en 1ª persona suele ocultarse el cuerpo completo del owner (para evitar clipping)
    /// y mostrar solo brazos; para el resto de jugadores sí se muestra el cuerpo y armas 3ª persona.
    /// </para>
    /// <para>
    /// Este componente corre al spawnear en red y también en <see cref="OnValidate"/> (útil para probar en editor).
    /// </para>
    /// </summary>
    public class LocalVisibility : NetworkBehaviour
    {
        [Tooltip("Si está desactivado, solo verás tu sombra. Si está activado, verás tu cuerpo entero.")]
        /// <summary>Si true, el owner ve su propio cuerpo; si false, solo sombras.</summary>
        public bool ShowBodyToOwner = false;

        [Tooltip("Arrastra aquí el/los 'Skinned Mesh Renderer' de tu personaje de Mixamo")]
        /// <summary>Renderers del cuerpo (Mixamo/3ª persona) sobre los que se ajusta ShadowCastingMode.</summary>
        public SkinnedMeshRenderer[] BodyRenderers;

        [Tooltip("Arrastra aquí los brazos sueltos")]
        /// <summary>Brazos de 1ª persona: se activan solo para el owner.</summary>
        public GameObject ArmsRenderers;

        [Tooltip("Arrastra aquí el contenedor de armas en tercera persona")]
        /// <summary>Contenedor de armas fake 3ª persona: se muestra (o solo sombras) según owner.</summary>
        public GameObject ThirdPersonWeapons;

        // Se ejecuta en el momento en que el jugador aparece en la red
        public override void OnNetworkSpawn()
        {
            UpdateVisibility();
        }

        // OnValidate nos permite que, si haces clic en el botón en el Inspector mientras juegas, se actualice al instante
        void OnValidate()
        {
            if (IsSpawned)
            {
                UpdateVisibility();
            }
        }

        void UpdateVisibility()
        {
            // Regla:
            // - Owner: cuerpo y armas 3ª persona se muestran completos o solo sombras según ShowBodyToOwner.
            // - No-owner: cuerpo y armas 3ª persona siempre visibles.
            // - Brazos 1ª persona: solo owner.
            // 1. Gestionar el cuerpo de Mixamo
            foreach (var renderer in BodyRenderers)
            {
                if (renderer != null)
                {
                    if (IsOwner)
                    {
                        // Si somos el dueño, decidimos si lo vemos o solo vemos la sombra
                        renderer.shadowCastingMode = ShowBodyToOwner ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
                    }
                    else
                    {
                        // Para el resto de jugadores en la red, nuestro cuerpo SIEMPRE es visible
                        renderer.shadowCastingMode = ShadowCastingMode.On;
                    }
                }
            }

            // 2. Gestionar las armas de tercera persona (Falsas)
            if (ThirdPersonWeapons != null)
            {
                // Obtenemos todos los renderers de las armas, incluso de las que están apagadas internamente (true)
                Renderer[] tpWeaponRenderers = ThirdPersonWeapons.GetComponentsInChildren<Renderer>(true);

                foreach (var renderer in tpWeaponRenderers)
                {
                    if (IsOwner)
                    {
                        // Para ti (el dueño): Las armas físicas siguen ahí y funcionan, pero solo ves su sombra
                        renderer.shadowCastingMode = ShowBodyToOwner ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
                    }
                    else
                    {
                        // Para los demás: Las ven perfectamente
                        renderer.shadowCastingMode = ShadowCastingMode.On;
                    }
                }
            }

            // 3. Gestionar los brazos flotantes de primera persona
            if (ArmsRenderers != null)
            {
                if (IsOwner)
                {
                    // Solo tú ves tus brazos flotantes
                    ArmsRenderers.SetActive(true);
                }
                else
                {
                    // Los demás no ven tus brazos flotantes
                    ArmsRenderers.SetActive(false);
                }
            }
        }
    }
}