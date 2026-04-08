using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    public class JetpackCounter : MonoBehaviour
    {
        [Tooltip("Image component representing jetpack fuel")]
        public Image JetpackFillImage;

        [Tooltip("Canvas group that contains the whole UI for the jetack")]
        public CanvasGroup MainCanvasGroup;

        [Tooltip("Component to animate the color when empty or full")]
        public FillBarColorChange FillBarColorChange;

        Jetpack m_Jetpack;

        void Awake()
        {
            // En multiplayer/MPPM el jetpack puede no existir aún cuando carga el HUD.
            m_Jetpack = FindFirstObjectByType<Jetpack>();
            if (FillBarColorChange != null)
                FillBarColorChange.Initialize(1f, 0f);
        }

        void Update()
        {
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