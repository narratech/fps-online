using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;



namespace Unity.FPS.UI
{
    /// <summary>
    /// UI local del combustible del jetpack adaptada a multijugador (solo Owner).
    /// <para>
    /// Este HUD se actualiza leyendo <see cref="Jetpack.CurrentFillRatio"/> y mostrando/ocultando el panel
    /// según <see cref="Jetpack.IsJetpackUnlocked"/>.
    /// </para>
    /// <para>
    /// DEFECTUOSO (resolución de referencia): usa <see cref="Object.FindFirstObjectByType{T}"/> para encontrar
    /// un <see cref="Jetpack"/> en la escena. En multijugador puede devolver el jetpack de otro jugador o de un bot,
    /// dependiendo de orden de aparición. Lo correcto sería resolver el jetpack del <b>player local owner</b>
    /// (p.ej., desde el NetworkObject local o pasando referencia desde el prefab/HUD).
    /// </para>
    /// </summary>
    public class ClientJetpackCounter : NetworkBehaviour
    {
        [Tooltip("Image component representing jetpack fuel")]
        /// <summary>Imagen cuyo <see cref="Image.fillAmount"/> representa el ratio de combustible.</summary>
        public Image JetpackFillImage;

        [Tooltip("Canvas group that contains the whole UI for the jetack")]
        /// <summary>Contenedor principal del HUD (se activa/desactiva según desbloqueo).</summary>
        public CanvasGroup MainCanvasGroup;

        [Tooltip("Component to animate the color when empty or full")]
        /// <summary>Efecto visual para cambiar color según nivel (vacío/lleno).</summary>
        public FillBarColorChange FillBarColorChange;

        /// <summary>Referencia al jetpack que alimenta la UI (idealmente el del owner).</summary>
        Jetpack m_Jetpack;

        void Awake()
        {
            // En multiplayer/MPPM el jetpack puede no existir aún cuando carga el HUD.
            if (FillBarColorChange != null)
                FillBarColorChange.Initialize(1f, 0f);
        }


        public override void OnNetworkSpawn()
        {


            base.OnNetworkSpawn();

            if (IsOwner)
            {
                // Nada especial: el binding se hace de forma perezosa en Update.
                if (FillBarColorChange != null)
                    FillBarColorChange.Initialize(1f, 0f);
            }
            else
            {
                enabled = false;
            }


        }




        void Update()
        {
            if (!IsOwner) return;

            // Binding perezoso del Jetpack (ver observación DEFECTUOSO arriba).
            if (m_Jetpack == null)
            {
                m_Jetpack = FindFirstObjectByType<Jetpack>();
                if (m_Jetpack == null)
                {
                    if (MainCanvasGroup != null) MainCanvasGroup.gameObject.SetActive(false);
                    return;
                }
            }

            if (MainCanvasGroup != null)
                MainCanvasGroup.gameObject.SetActive(m_Jetpack.IsJetpackUnlocked);

            if (m_Jetpack.IsJetpackUnlocked)
            {
                if (JetpackFillImage != null)
                    JetpackFillImage.fillAmount = m_Jetpack.CurrentFillRatio;
                if (FillBarColorChange != null)
                    FillBarColorChange.UpdateVisual(m_Jetpack.CurrentFillRatio);
            }
        }
    }
}
