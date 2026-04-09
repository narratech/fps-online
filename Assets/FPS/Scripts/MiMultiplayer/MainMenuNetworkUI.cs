using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace HelloWorld
{
    public class MainMenuNetworkUI : MonoBehaviour
    {
        public static string PlayerNickname = "Player";

        [Header("Configuración de Personajes")]
        public NetworkPrefabsList UniversidadesList;

        public GameObject controlsImageObject;
        private InputAction cancelAction;

        // Contenedores
        private VisualElement mainMenuContainer;
        private VisualElement roleSelectionContainer;
        private VisualElement connectionContainer;

        // Botones principales
        private Button btnPlay;
        private Button btnControls;
        private Button btnStartHost;
        private Button btnStartClient;
        private Button btnFinalStart;

        // Botones de Atrás
        private Button btnBackRole;
        private Button btnBackConnection;

        // Campos de texto
        private TextField inputNickname;
        private TextField inputIP;
        private TextField inputPort;
        private DropdownField dropdownCharacter;

        // Etiqueta para mostrar los errores
        private Label lblError;

        private bool isConnectingAsHost;

        void OnEnable()
        {
            var uiDocument = GetComponent<UIDocument>();
            var root = uiDocument.rootVisualElement;

            mainMenuContainer = root.Q<VisualElement>("MainMenuContainer");
            roleSelectionContainer = root.Q<VisualElement>("RoleSelectionContainer");
            connectionContainer = root.Q<VisualElement>("ConnectionContainer");

            btnPlay = root.Q<Button>("BtnPlay");
            btnControls = root.Q<Button>("BtnControls");
            btnStartHost = root.Q<Button>("BtnStartHost");
            btnStartClient = root.Q<Button>("BtnStartClient");
            btnFinalStart = root.Q<Button>("BtnFinalStart");

            btnBackRole = root.Q<Button>("BtnBackRole");
            btnBackConnection = root.Q<Button>("BtnBackConnection");

            inputNickname = root.Q<TextField>("InputNickname");
            inputIP = root.Q<TextField>("InputIP");
            inputPort = root.Q<TextField>("InputPort");
            dropdownCharacter = root.Q<DropdownField>("DropdownCharacter");

            lblError = root.Q<Label>("LblError");

            // Limitamos el campo de texto y recuperamos el último nombre guardado
            SetNicknameLimit(48);
            if (inputNickname != null && PlayerPrefs.HasKey("PlayerName"))
            {
                inputNickname.value = PlayerPrefs.GetString("PlayerName");
            }

            if (btnPlay != null) btnPlay.clicked += OnPlayClicked;
            if (btnControls != null) btnControls.clicked += OnControlsClicked;
            if (btnStartHost != null) btnStartHost.clicked += OnHostSelected;
            if (btnStartClient != null) btnStartClient.clicked += OnClientSelected;
            if (btnFinalStart != null) btnFinalStart.clicked += OnFinalStartClicked;

            if (btnBackRole != null) btnBackRole.clicked += OnBackRoleClicked;
            if (btnBackConnection != null) btnBackConnection.clicked += OnBackConnectionClicked;

            cancelAction = InputSystem.actions.FindAction("UI/Cancel");
            if (cancelAction != null) cancelAction.Enable();

            SetupCharacterDropdown();
            ShowContainer(mainMenuContainer);
            ClearError(); // Limpiamos errores al empezar
        }

        private void SetupCharacterDropdown()
        {
            if (dropdownCharacter != null && UniversidadesList != null)
            {
                List<string> characterNames = new List<string>();

                // Leemos directamente del archivo ScriptableObject de Netcode
                foreach (var networkPrefab in UniversidadesList.PrefabList)
                {
                    if (networkPrefab.Prefab != null)
                    {
                        characterNames.Add(networkPrefab.Prefab.name);
                    }
                }

                dropdownCharacter.choices = characterNames;

                if (characterNames.Count > 0)
                {
                    dropdownCharacter.index = 0; // Selecciona el primero por defecto
                }
            }
        }

        void Start()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectCallback;
            }
        }

        void OnDisable()
        {
            if (btnPlay != null) btnPlay.clicked -= OnPlayClicked;
            if (btnControls != null) btnControls.clicked -= OnControlsClicked;
            if (btnStartHost != null) btnStartHost.clicked -= OnHostSelected;
            if (btnStartClient != null) btnStartClient.clicked -= OnClientSelected;
            if (btnFinalStart != null) btnFinalStart.clicked -= OnFinalStartClicked;

            if (btnBackRole != null) btnBackRole.clicked -= OnBackRoleClicked;
            if (btnBackConnection != null) btnBackConnection.clicked -= OnBackConnectionClicked;

            if (cancelAction != null) cancelAction.Disable();

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectCallback;
            }
        }

        void Update()
        {
            if (controlsImageObject != null && controlsImageObject.activeSelf && cancelAction != null && cancelAction.WasPressedThisFrame())
            {
                controlsImageObject.SetActive(false);
            }
        }

        private void SetNicknameLimit(int maxLength)
        {
            if (inputNickname != null)
            {
                inputNickname.maxLength = maxLength;
            }
        }

        // --- LÓGICA DE TRANSICIONES DE INTERFAZ ---

        private void OnPlayClicked()
        {
            ClearError();
            ShowContainer(roleSelectionContainer);
        }

        private void OnControlsClicked()
        {
            if (controlsImageObject != null) controlsImageObject.SetActive(true);
        }

        private void OnHostSelected()
        {
            ClearError();
            isConnectingAsHost = true;
            if (inputIP != null) inputIP.style.display = DisplayStyle.Flex;
            if (inputPort != null) inputPort.style.display = DisplayStyle.Flex;
            ShowContainer(connectionContainer);
        }

        private void OnClientSelected()
        {
            ClearError();
            isConnectingAsHost = false;
            if (inputIP != null) inputIP.style.display = DisplayStyle.Flex;
            if (inputPort != null) inputPort.style.display = DisplayStyle.Flex;
            ShowContainer(connectionContainer);
        }

        private void OnBackRoleClicked()
        {
            ClearError();
            ShowContainer(mainMenuContainer);
        }

        private void OnBackConnectionClicked()
        {
            ClearError();
            ShowContainer(roleSelectionContainer);
        }

        private void ShowContainer(VisualElement containerToShow)
        {
            mainMenuContainer.style.display = DisplayStyle.None;
            roleSelectionContainer.style.display = DisplayStyle.None;
            connectionContainer.style.display = DisplayStyle.None;

            containerToShow.style.display = DisplayStyle.Flex;
        }

        // --- SISTEMA DE ERRORES ---
        private void ShowError(string message)
        {
            if (lblError != null)
            {
                lblError.text = message;
                lblError.style.display = DisplayStyle.Flex;
            }
            Debug.LogError(message);
        }

        private void ClearError()
        {
            if (lblError != null)
            {
                lblError.text = "";
                lblError.style.display = DisplayStyle.None;
            }
        }

        private void OnClientDisconnectCallback(ulong clientId)
        {
            // Si nos desconectan a nosotros (el cliente local)
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                // Apagamos la red para que no se quede pillado intentando conectar
                NetworkManager.Singleton.Shutdown();

                // Volvemos a mostrar el botón y mostramos el error
                ShowError("Connection failed. Check IP and Port, or Host is unreachable.");
            }
        }

        // --- LÓGICA DE RED (NETCODE) ---

        private void OnFinalStartClicked()
        {
            ClearError();

            // --- CONTROL DE NOMBRE VACÍO ---
            if (string.IsNullOrWhiteSpace(inputNickname.value))
            {
                ShowError("Please enter a nickname before connecting.");
                return; // Cortamos la función aquí, no le dejamos conectar
            }

            // Guardamos el nombre en PlayerPrefs para leerlo luego en la partida
            PlayerNickname = inputNickname.value;
            PlayerPrefs.SetString("PlayerName", PlayerNickname);
            PlayerPrefs.Save();

            // 1. Miramos qué número ha elegido en el desplegable
            int characterIndex = 0;
            if (dropdownCharacter != null)
            {
                characterIndex = dropdownCharacter.index;
            }

            // 2. Juntamos el nombre y el número con un guión bajo
            string payloadString = $"{PlayerNickname}_{characterIndex}";

            // 3. Lo metemos en el maletín y se lo damos al Network Manager
            byte[] payloadData = Encoding.ASCII.GetBytes(payloadString);
            NetworkManager.Singleton.NetworkConfig.ConnectionData = payloadData;

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                string ipAddress = string.IsNullOrEmpty(inputIP?.value) ? "127.0.0.1" : inputIP.value;

                if (isConnectingAsHost)
                {
                    transport.ConnectionData.ServerListenAddress = "0.0.0.0";
                    transport.ConnectionData.Address = ipAddress;
                }
                else
                {
                    transport.ConnectionData.Address = ipAddress;
                }

                ushort portNumber = 7777;
                if (inputPort != null && !string.IsNullOrEmpty(inputPort.value))
                {
                    if (ushort.TryParse(inputPort.value, out ushort parsedPort)) portNumber = parsedPort;
                }
                transport.ConnectionData.Port = portNumber;
            }

            // --- CONTROL DE FALLOS AL INICIAR ---
            if (isConnectingAsHost)
            {
                try
                {
                    // Intentamos arrancar el Host
                    if (NetworkManager.Singleton.StartHost())
                    {
                        // LA ESCENA QUE SE CARGA NADA MÁS EMPEZAR
                        NetworkManager.Singleton.SceneManager.LoadScene("PrisonScene", UnityEngine.SceneManagement.LoadSceneMode.Single);          
                    }
                    else
                    {
                        ShowError("Failed to start Host. Check IP and Port.");
                        NetworkManager.Singleton.Shutdown();
                    }
                }
                catch (System.Exception e)
                {
                    // Si el puerto ya está en uso, UTP lanzará una excepción y la pillamos aquí
                    ShowError("Host Error: Port already in use. Try a different port.");
                    NetworkManager.Singleton.Shutdown();
                }
            }
            else
            {
                // El cliente empieza a conectar. Si falla, el evento OnClientDisconnectCallback hará el resto.
                if (lblError != null)
                {
                    lblError.text = "Connecting...";
                    lblError.style.color = new StyleColor(Color.yellow); // Mensaje temporal en amarillo
                    lblError.style.display = DisplayStyle.Flex;
                }
                NetworkManager.Singleton.StartClient();
            }
        }
    }
}