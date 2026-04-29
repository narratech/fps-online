using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Conteo agregado por votante: "quién votó" y "cuántas veces".
/// </summary>
[Serializable]
public class ConteoVoto
{
    /// <summary>ClientId (NGO) del votante.</summary>
    public ulong idVotante;
    /// <summary>Número de votos recibidos desde ese votante.</summary>
    public int totalVotos;
}

/// <summary>
/// DTO (data transfer object) exportable por jugador para persistencia/analítica.
/// </summary>
[Serializable]
public class PlayerDataExport
{
    /// <summary>ClientId del jugador (objetivo).</summary>
    public ulong clientId;
    /// <summary>Nombre lógico del jugador (según menú/approval).</summary>
    public string playerName;
    /// <summary>Lista agregada de votos "Humano" recibidos.</summary>
    public List<ConteoVoto> votosHumano;
    /// <summary>Lista agregada de votos "Robot" recibidos.</summary>
    public List<ConteoVoto> votosRobot;
}

/// <summary>
/// Wrapper exportable para serialización JSON de resultados completos de partida.
/// </summary>
[Serializable]
public class MatchDataWrapperExport
{
    /// <summary>Resultados por jugador (objetivo) de la partida.</summary>
    public List<PlayerDataExport> resultadosPartida = new List<PlayerDataExport>();
}

/// <summary>
/// Estructura interna de almacenamiento por jugador durante la partida (no serializable directamente a JSON).
/// </summary>
public class PlayerData
{
    /// <summary>ClientId del jugador (objetivo).</summary>
    public ulong clientId;
    /// <summary>Nombre del jugador.</summary>
    public string playerName;
    /// <summary>Historial (con repetidos) de quién le votó como Humano.</summary>
    public List<ulong> votosHumanoRecibidos = new List<ulong>();
    /// <summary>Historial (con repetidos) de quién le votó como Robot.</summary>
    public List<ulong> votosRobotRecibidos = new List<ulong>();
}

/// <summary>
/// Singleton de partida para registrar jugadores y votos, y opcionalmente exportar a JSON al finalizar.
/// <para>
/// Integración:
/// - Se alimenta desde `ServerConnectionHandler.RegisterPlayer(...)` durante el approval.
/// - Se alimenta desde `PlayerVotingSync` cuando se envían votos al servidor.
/// </para>
/// <para>
/// Autoridad: se pretende que el servidor sea la única fuente de verdad (ver <see cref="ExportarDatosAJson"/>).
/// </para>
/// <para>
/// DEFECTUOSO (persistencia en Assets): la exportación apunta a <see cref="Application.dataPath"/>, que en editor
/// cae dentro de `Assets/` y puede ensuciar el repo con JSONs versionables. Está desactivado correctamente,
/// pero si se reactiva conviene escribir fuera del proyecto (persistentDataPath) o en una carpeta ignorada por git.
/// </para>
/// </summary>
public class MatchDataManager : MonoBehaviour
{
    /// <summary>Instancia única viva en escena (patrón singleton simple).</summary>
    public static MatchDataManager Instance { get; private set; }

    /// <summary>Mapa clientId → datos acumulados durante la partida.</summary>
    private Dictionary<ulong, PlayerData> playersData = new Dictionary<ulong, PlayerData>();

    void Awake()
    {
        // Singleton simple: conserva la primera instancia y destruye duplicados.
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); }
    }

    void Start()
    {
        // Solo el servidor registra automáticamente al host en el arranque de red.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer) 
        {
            NetworkManager.Singleton.OnServerStarted += RegistrarHost;

            if (NetworkManager.Singleton.IsListening)
            {
                RegistrarHost();
            } 
        }
    }

    private void RegistrarHost()
    {
        // El host tiene LocalClientId en el proceso servidor (host).
        ulong hostId = NetworkManager.Singleton.LocalClientId;

        // Nombre local del host (si se usa el mismo PlayerPrefs que el menú).
        string hostName = PlayerPrefs.GetString("PlayerName", "Host_Desconocido");

        RegisterPlayer(hostId, hostName);
    }

    /// <summary>
    /// Registra (o actualiza) un jugador en el agregado de datos.
    /// </summary>
    /// <param name="clientId">ClientId del jugador.</param>
    /// <param name="playerName">Nombre lógico.</param>
    public void RegisterPlayer(ulong clientId, string playerName)
    {
        if (!playersData.ContainsKey(clientId))
        {
            playersData[clientId] = new PlayerData { clientId = clientId, playerName = playerName };
        }
        else
        {
            // Si el jugador ya existía (quizás por el registro de emergencia), actualizamos su nombre real
            if (playersData[clientId].playerName.StartsWith("VirtualPlayer_"))
            {
                playersData[clientId].playerName = playerName;
            }
        }
    }

    /// <summary>
    /// Registra un voto de <paramref name="tiradorId"/> hacia <paramref name="objetivoId"/>.
    /// </summary>
    /// <param name="tiradorId">Quién vota.</param>
    /// <param name="objetivoId">Quién recibe el voto.</param>
    /// <param name="esVotoHumano">True = voto "Humano"; false = voto "Robot".</param>
    public void RegistrarVoto(ulong tiradorId, ulong objetivoId, bool esVotoHumano)
    {
        // Registro de emergencia para el objetivo
        if (!playersData.ContainsKey(objetivoId))
        {
            RegisterPlayer(objetivoId, "VirtualPlayer_" + objetivoId);
        }

        // Registro de emergencia para el tirador
        if (!playersData.ContainsKey(tiradorId))
        {
            RegisterPlayer(tiradorId, "VirtualPlayer_" + tiradorId);
        }

        PlayerData datosObjetivo = playersData[objetivoId];

        if (esVotoHumano)
        {
            datosObjetivo.votosHumanoRecibidos.Add(tiradorId);
        }
        else
        {
            datosObjetivo.votosRobotRecibidos.Add(tiradorId);
        }

    }

    /// <summary>
    /// Exporta los datos agregados a un JSON en disco (solo servidor).
    /// </summary>
    /// <remarks>
    /// Actualmente la escritura real está comentada para no generar ficheros dentro de `Assets/`.
    /// </remarks>
    public void ExportarDatosAJson()
    {
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer) return;
        if (playersData.Count == 0) return;

        MatchDataWrapperExport wrapperExport = new MatchDataWrapperExport();

        foreach (var data in playersData.Values)
        {
            List<ConteoVoto> humanosAgrupados = data.votosHumanoRecibidos
                .GroupBy(voto => voto)
                .Select(grupo => new ConteoVoto { idVotante = grupo.Key, totalVotos = grupo.Count() })
                .ToList();

            List<ConteoVoto> robotsAgrupados = data.votosRobotRecibidos
                .GroupBy(voto => voto)
                .Select(grupo => new ConteoVoto { idVotante = grupo.Key, totalVotos = grupo.Count() })
                .ToList();

            PlayerDataExport exportData = new PlayerDataExport
            {
                clientId = data.clientId,
                playerName = data.playerName,
                votosHumano = humanosAgrupados,
                votosRobot = robotsAgrupados
            };

            wrapperExport.resultadosPartida.Add(exportData);
        }

        string json = JsonUtility.ToJson(wrapperExport, true);
        string timeStamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string filePath = Path.Combine(Application.dataPath, $"ResultadosPartida_{timeStamp}.json");

        // Desactivado: generaba archivos ResultadosPartida_*.json en Assets/ al cerrar la partida (datos de votación / análisis).
        // Reactiva estas líneas cuando quieras volver a exportar a disco.
        // File.WriteAllText(filePath, json);
        // Debug.Log($"<color=green>¡Partida guardada! JSON creado en: {filePath}</color>");
    }

    public void OnDestroy()
    {
        // Exportación JSON al salir (misma función que arriba). Comentado junto con File.WriteAllText.
        // ExportarDatosAJson();
    }
}