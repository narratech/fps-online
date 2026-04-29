using System;
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
    /// <summary>
    /// Texto TMP donde se renderiza el marcador. Si es null, se crea en <see cref="EnsureUI"/>.
    /// </summary>
    [SerializeField] TextMeshProUGUI scoreboardText;

    [Header("Update")]
    /// <summary>Cadencia del refresco (en tiempo no escalado).</summary>
    [SerializeField] float refreshIntervalSeconds = 0.25f;

    /// <summary>Próximo tick de refresco.</summary>
    float m_NextRefreshTime;
    /// <summary>Buffer para construir el texto sin asignaciones por frame.</summary>
    readonly StringBuilder m_Sb = new StringBuilder(512);

    /// <summary>Cache global de la fuente Roboto usada por el HUD.</summary>
    static TMP_FontAsset s_CachedRobotoHudFont;

    /// <summary>
    /// Busca y cachea la fuente Roboto usada en el HUD del sample.
    /// </summary>
    static TMP_FontAsset ResolveGameHudFont()
    {
        if (s_CachedRobotoHudFont != null) return s_CachedRobotoHudFont;
        var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < fonts.Length; i++)
        {
            var f = fonts[i];
            if (f != null && string.Equals(f.name, "Roboto-Black SDF", StringComparison.Ordinal))
            {
                s_CachedRobotoHudFont = f;
                break;
            }
        }

        return s_CachedRobotoHudFont;
    }

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

    /// <summary>
    /// Refresca el marcador leyendo todos los <see cref="PlayerStats"/> de escena.
    /// </summary>
    void Update()
    {
        if (!IsOwner) return;
        if (Time.unscaledTime < m_NextRefreshTime) return;
        m_NextRefreshTime = Time.unscaledTime + refreshIntervalSeconds;

        EnsureUI();
        if (scoreboardText == null) return;

        var allStats = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);

        m_Sb.Clear();

        for (int i = 0; i < allStats.Length; i++)
        {
            var s = allStats[i];
            string name = s.PlayerName.Value.ToString();
            if (string.IsNullOrWhiteSpace(name))
                name = $"Player {s.OwnerClientId}";

            m_Sb.Append(name);
            m_Sb.Append("  Kills: ");
            m_Sb.Append(s.Kills.Value);
            m_Sb.Append("  Deaths: ");
            m_Sb.Append(s.Deaths.Value);
            m_Sb.AppendLine();
        }

        scoreboardText.text = m_Sb.ToString();
    }

    /// <summary>
    /// Crea el texto del marcador en el Canvas del jugador local (si existe en el prefab).
    /// </summary>
    void EnsureUI()
    {
        if (scoreboardText != null) return;

        // Buscamos un Canvas en el prefab del jugador (ya existe en Human_Prefab).
        var canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null) return;

        var go = new GameObject("ScoreboardText", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-16f, -16f);
        rt.sizeDelta = new Vector2(640f, 420f);

        scoreboardText = go.AddComponent<TextMeshProUGUI>();
        var hudFont = ResolveGameHudFont();
        if (hudFont != null)
            scoreboardText.font = hudFont;
        else if (TMP_Settings.defaultFontAsset != null)
            scoreboardText.font = TMP_Settings.defaultFontAsset;

        scoreboardText.fontSize = 18;
        scoreboardText.fontStyle = FontStyles.Normal;
        scoreboardText.enableWordWrapping = true;
        scoreboardText.alignment = TextAlignmentOptions.TopRight;
        scoreboardText.text = "";
    }
}

