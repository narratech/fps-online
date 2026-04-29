using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using TMPro; 

/// <summary>
/// Etiqueta de nombre sobre la cabeza + contadores de kills/muertes (NetworkVariables).
/// <para>
/// Red:
/// - <see cref="NetworkedName"/> es <b>Owner-write</b> para que el jugador local pueda publicar su nickname.
/// - <see cref="Kills"/> y <see cref="Deaths"/> son <b>Server-write</b> para que la puntuación sea autoritativa.
/// </para>
/// <para>
/// Nota: en este proyecto también existe `PlayerStats` como scoreboard server-write; esto se puede considerar
/// redundante si se usan ambos a la vez.
/// </para>
/// </summary>
public class PlayerNameTag : NetworkBehaviour
{
    [Tooltip("Arrastra aquí el componente TextMeshPro que está sobre la cabeza")]
    /// <summary>
    /// Referencia al texto (WorldSpace) que se orienta hacia la cámara y muestra el nombre.
    /// </summary>
    public TextMeshProUGUI NameText;

    // Nombre en red (hasta ~60 caracteres ASCII; alineado con el límite del menú).
    /// <summary>
    /// Nickname en red. El owner lo escribe (desde PlayerPrefs) y todos lo leen.
    /// </summary>
    public NetworkVariable<FixedString64Bytes> NetworkedName = new NetworkVariable<FixedString64Bytes>(
        "",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    /// <summary>Kills autoritativas (solo servidor escribe).</summary>
    public NetworkVariable<int> Kills = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// <summary>Muertes autoritativas (solo servidor escribe).</summary>
    public NetworkVariable<int> Deaths = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // Llamado por enemigos al morir (servidor) vía SendMessage desde el asmdef de AI.
    /// <summary>Incrementa kills en servidor (útil para hooks vía SendMessage).</summary>
    public void AddKillServer()
    {
        if (!IsServer) return;
        Kills.Value++;
    }

    // Llamado por el respawn al morir (servidor) si quieres reutilizarlo vía SendMessage.
    /// <summary>Incrementa muertes en servidor (útil para hooks vía SendMessage).</summary>
    public void AddDeathServer()
    {
        if (!IsServer) return;
        Deaths.Value++;
    }

    /// <summary>
    /// Suscripción local a cambios de nombre. (En NGO, la suscripción funciona una vez la variable existe en red.)
    /// </summary>
    void Start()
    {
        // Nos suscribimos para escuchar cuando internet cambie el nombre
        NetworkedName.OnValueChanged += OnNameChanged;
    }

    /// <summary>
    /// Al spawnear:
    /// - Owner publica su nombre desde PlayerPrefs y oculta su propia etiqueta (no se ve a sí mismo).
    /// - No-owner actualiza visualmente el texto con el valor ya replicado.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Si somos el dueño, leemos nuestro nombre guardado y lo subimos a la red
            string savedName = PlayerPrefs.GetString("PlayerName", "Player");
            NetworkedName.Value = new FixedString64Bytes(savedName);
            NameText.gameObject.SetActive(false);
        }
        else
        {
            // Si es un enemigo que ya estaba en la partida, forzamos que se muestre su nombre
            UpdateNameTag(NetworkedName.Value.ToString());
        }
    }

    /// <summary>Desuscripción para evitar callbacks hacia objetos destruidos.</summary>
    public override void OnNetworkDespawn()
    {
        NetworkedName.OnValueChanged -= OnNameChanged;
    }

    /// <summary>Callback cuando cambia el nombre en red.</summary>
    void OnNameChanged(FixedString64Bytes previousValue, FixedString64Bytes newValue)
    {
        UpdateNameTag(newValue.ToString());
    }

    /// <summary>Aplica el string al TMP.</summary>
    void UpdateNameTag(string newName)
    {
        if (NameText != null)
        {
            NameText.text = newName;
        }
    }

    // --- EFECTO BILLBOARD (Mirar a la cámara) ---
    /// <summary>
    /// Hace que la etiqueta mire a la cámara principal.
    /// <para>
    /// DEFECTUOSO (rendimiento/robustez): usar <see cref="Camera.main"/> en cada frame puede ser caro y además
    /// depende de que el tag MainCamera esté bien configurado. Si hay varias cámaras, puede apuntar a una no deseada.
    /// Lo ideal es cachear la cámara local relevante o usar un sistema de UI/billboard central.
    /// </para>
    /// </summary>
    void LateUpdate()
    {
        // Si hay un texto asignado y hay una cámara principal en la escena
        if (NameText != null && Camera.main != null)
        {
            // Hacemos que el cartel mire exactamente hacia la misma dirección que la cámara principal
            NameText.transform.rotation = Camera.main.transform.rotation;
        }
    }
}