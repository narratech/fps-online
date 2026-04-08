using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// HUD local que muestra el marcador (nombre, kills, muertes) de TODOS los jugadores.
/// Se habilita solo para el jugador dueño.
/// </summary>
public class ClientScoreboardHUD : NetworkBehaviour
{
    [Header("UI")]
    [SerializeField] TextMeshProUGUI scoreboardText;

    [Header("Update")]
    [SerializeField] float refreshIntervalSeconds = 0.25f;

    float m_NextRefreshTime;
    readonly StringBuilder m_Sb = new StringBuilder(512);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        EnsureUI();
    }

    void Update()
    {
        if (!IsOwner) return;
        if (Time.unscaledTime < m_NextRefreshTime) return;
        m_NextRefreshTime = Time.unscaledTime + refreshIntervalSeconds;

        EnsureUI();
        if (scoreboardText == null) return;

        var allStats = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);

        m_Sb.Clear();
        m_Sb.AppendLine("MARCADOR");

        for (int i = 0; i < allStats.Length; i++)
        {
            var s = allStats[i];
            string name = s.PlayerName.Value.ToString();
            if (string.IsNullOrWhiteSpace(name))
                name = $"Player {s.OwnerClientId}";

            m_Sb.Append(name);
            m_Sb.Append("  K:");
            m_Sb.Append(s.Kills.Value);
            m_Sb.Append("  D:");
            m_Sb.Append(s.Deaths.Value);
            m_Sb.AppendLine();
        }

        scoreboardText.text = m_Sb.ToString();
    }

    void EnsureUI()
    {
        if (scoreboardText != null) return;

        // Buscamos un Canvas en el prefab del jugador (ya existe en Human_Prefab).
        var canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null) return;

        // Creamos un texto simple arriba-izquierda.
        var go = new GameObject("ScoreboardText", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(20f, -20f);
        rt.sizeDelta = new Vector2(520f, 420f);

        scoreboardText = go.AddComponent<TextMeshProUGUI>();
        scoreboardText.fontSize = 22;
        scoreboardText.enableWordWrapping = false;
        scoreboardText.alignment = TextAlignmentOptions.TopLeft;
        scoreboardText.text = "MARCADOR\n";
    }
}

