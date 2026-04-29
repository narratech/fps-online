using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace LoseMenu
{
    /// <summary>
    /// Menú de derrota (UI Toolkit) con acciones "Main Menu" y "Play Again".
    /// <para>
    /// Multijugador:
    /// - Un cliente puro no puede reiniciar la partida para todos, por eso se oculta el botón "Play Again".
    /// - Volver al menú cierra la sesión de red (<see cref="NetworkManager.Shutdown"/>) y destruye el NetworkManager
    ///   para que el menú inicial cree uno limpio.
    /// </para>
    /// </summary>
    public class LoseMenuManager : MonoBehaviour
    {
        /// <summary>Botón "Main Menu".</summary>
        private Button btnMainMenu;
        /// <summary>Botón "Play Again" (solo host).</summary>
        private Button btnPlayAgain;

        /// <summary>Contenedor principal del menú.</summary>
        private VisualElement menuContainer;

        /// <summary>Wire-up de UI y reglas de rol (host/client) al habilitar.</summary>
        private void OnEnable()
        {
            var uiDocument = GetComponent<UIDocument>();
            var root = uiDocument.rootVisualElement;

            menuContainer = root.Q<VisualElement>("MenuContainer");

            btnMainMenu = root.Q<Button>("BtnMainMenu");
            btnPlayAgain = root.Q<Button>("BtnPlayAgain");

            if (btnMainMenu != null) btnMainMenu.clicked += OnMainMenuClicked;
            if (btnPlayAgain != null) btnPlayAgain.clicked += OnPlayAgainClicked;

            // --- LÓGICA DE ROLES AL INICIAR EL MENÚ ---
            if (NetworkManager.Singleton != null)
            {
                if (NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsHost)
                {
                    // Si es un cliente puro (no es el Host), ocultamos el botón de volver a jugar
                    // porque un cliente no puede obligar al servidor a reiniciar la partida.
                    if (btnPlayAgain != null)
                    {
                        btnPlayAgain.style.display = DisplayStyle.None;
                    }
                }
            }
        }

        private void OnDisable()
        {
            // Limpieza de callbacks.
            if (btnMainMenu != null) btnMainMenu.clicked -= OnMainMenuClicked;
            if (btnPlayAgain != null) btnPlayAgain.clicked -= OnPlayAgainClicked;
        }

        /// <summary>
        /// Sale de la sesión de red y vuelve al menú inicial localmente.
        /// </summary>
        private void OnMainMenuClicked()
        {
            // Verificamos si hay una partida activa
            if (NetworkManager.Singleton != null)
            {
                if (NetworkManager.Singleton.IsHost)
                {
                    Debug.Log("Soy Host: Cerrando el servidor y volviendo al menú.");
                }
                else if (NetworkManager.Singleton.IsClient)
                {
                    Debug.Log("Soy Cliente: Desconectándome y volviendo al menú.");
                }

                // 1. Apagamos la red (desconecta a todos o te saca de la partida)
                NetworkManager.Singleton.Shutdown();

                // 2. Destruimos el NetworkManager para que el IntroMenu cree uno nuevo y limpio
                Destroy(NetworkManager.Singleton.gameObject);
            }

            // 3. Cargamos la escena de IntroMenu de forma local, no por red
            SceneManager.LoadScene("IntroMenu");
        }

        /// <summary>
        /// Solo host: reinicia la partida para todos cargando escena vía NGO SceneManager.
        /// </summary>
        private void OnPlayAgainClicked()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
            {
                Debug.Log("Soy Host: Reiniciando la partida para todos.");
                // Usamos el SceneManager de Netcode para llevar a todo el grupo de vuelta al mapa
                NetworkManager.Singleton.SceneManager.LoadScene("MainScene", LoadSceneMode.Single);
            }
        }
    }
}