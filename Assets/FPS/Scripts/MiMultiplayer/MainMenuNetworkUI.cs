using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace HelloWorld
{
    /// <summary>
    /// UI Toolkit del menú principal para arrancar Host/Client con NGO, seleccionar personaje y nickname.
    /// <para>
    /// Flujo:
    /// - El usuario elige nombre + prefab (por índice en <see cref="UniversidadesList"/>).
    /// - Se empaqueta en <see cref="NetworkConfig.ConnectionData"/> como `"nickname_index"`.
    /// - Al conectar, el servidor lo parsea en `ServerConnectionHandler` (ConnectionApproval) y asigna prefab.
    /// </para>
    /// <para>
    /// DEFECTUOSO (encoding): el payload se codifica en ASCII. Si se permite introducir caracteres no ASCII,
    /// se perderán o se transformarán. Si necesitáis nombres con tildes/Unicode, conviene UTF8.
    /// (Marcado solo como comentario: no se cambia código).
    /// </para>
    /// <para>
    /// Nota: este script también gestiona UI de "Controls" y navegación atrás, y muestra errores de conexión.
    /// </para>
    /// </summary>
    public class MainMenuNetworkUI : MonoBehaviour
    {
        /// <summary>
        /// Nickname global elegido. Se usa por otros scripts como fallback / lectura en partida.
        /// </summary>
        public static string PlayerNickname = "Player";

        [Header("Configuración de Personajes")]
        /// <summary>
        /// Lista de prefabs seleccionables para el dropdown (NGO NetworkPrefabsList).
        /// </summary>
        public NetworkPrefabsList UniversidadesList;

        /// <summary>Imagen/panel de controles (se muestra/oculta con Cancel).</summary>
        public GameObject controlsImageObject;
        /// <summary>Acción UI/Cancel del InputSystem (para cerrar controles).</summary>
        private InputAction cancelAction;

        // Contenedores
        /// <summary>Contenedor principal del menú.</summary>
        private VisualElement mainMenuContainer;
        /// <summary>Contenedor de selección de rol (host/client).</summary>
        private VisualElement roleSelectionContainer;
        /// <summary>Contenedor de conexión (ip/puerto + start).</summary>
        private VisualElement connectionContainer;

        // Botones principales
        /// <summary>Botón "Play" (entra a selección de rol).</summary>
        private Button btnPlay;
        /// <summary>Botón "Controls" (muestra panel de controles).</summary>
        private Button btnControls;
        /// <summary>Botón "Host".</summary>
        private Button btnStartHost;
        /// <summary>Botón "Client".</summary>
        private Button btnStartClient;
        /// <summary>Botón final para arrancar conexión con datos seleccionados.</summary>
        private Button btnFinalStart;

        // Botones de Atrás
        /// <summary>Volver desde selección de rol a menú principal.</summary>
        private Button btnBackRole;
        /// <summary>Volver desde conexión a selección de rol.</summary>
        private Button btnBackConnection;

        // Campos de texto
        /// <summary>Input del nickname.</summary>
        private TextField inputNickname;
        /// <summary>Input IP (host o destino).</summary>
        private TextField inputIP;
        /// <summary>Input puerto.</summary>
        private TextField inputPort;
        /// <summary>Dropdown de personaje (se llena desde <see cref="UniversidadesList"/>).</summary>
        private DropdownField dropdownCharacter;

        // Etiqueta para mostrar los errores
        /// <summary>Label de error/estado de conexión.</summary>
        private Label lblError;

        /// <summary>
        /// True si la acción final debe arrancar host; false si debe arrancar client.
        /// </summary>
        private bool isConnectingAsHost;

        /// <summary>
        /// Inicializa referencias de UI Toolkit y suscribe callbacks a botones/acciones.
        /// </summary>
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

        /// <summary>
        /// Rellena el dropdown de personajes usando los prefabs registrados en <see cref="UniversidadesList"/>.
        /// </summary>
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
            // Se engancha a desconexión para mostrar un error si falla conectar.
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectCallback;
            }
        }

        /// <summary>Desuscribe callbacks para evitar fugas al desactivar el menú.</summary>
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
            // Si el panel de controles está visible, Cancel lo cierra.
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

        /// <summary>Avanza a selección de rol.</summary>
        private void OnPlayClicked()
        {
            ClearError();
            ShowContainer(roleSelectionContainer);
        }

        /// <summary>Muestra la imagen/panel de controles.</summary>
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
        /// <summary>Muestra un error en UI y lo loguea.</summary>
        private void ShowError(string message)
        {
            if (lblError != null)
            {
                lblError.text = message;
                lblError.style.display = DisplayStyle.Flex;
            }
            Debug.LogError(message);
        }

        /// <summary>Limpia el error en UI.</summary>
        private void ClearError()
        {
            if (lblError != null)
            {
                lblError.text = "";
                lblError.style.display = DisplayStyle.None;
            }
        }

        /// <summary>
        /// Callback de desconexión: si el cliente local se desconecta durante conexión, se asume fallo.
        /// </summary>
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

        /// <summary>
        /// Construye el payload, configura transporte (ip/puerto) y arranca Host o Client.
        /// </summary>
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