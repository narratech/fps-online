using UnityEngine;
using Unity.Netcode.Components;
using static UnityEngine.UI.GridLayoutGroup;

/// <summary>
/// `NetworkTransform` con selección dinámica de autoridad (server vs owner) según tipo de entidad.
/// <para>
/// Intención de red:
/// - Jugadores humanos: movimiento local inmediato (owner-authoritative) para minimizar input latency.
/// - Bots (IA): movimiento simulado en servidor (server-authoritative) para consistencia y antitrampas.
/// </para>
/// <para>
/// Heurística actual: si el objeto tiene un componente <see cref="FSM"/>, se trata como bot.
/// </para>
/// <para>
/// Nota: este script asume que "bot == tiene FSM". Si mañana hay otros NPCs sin FSM, o humanos con FSM
/// para entrenamiento, la heurística puede fallar (revisar criterio).
/// </para>
/// </summary>
public class ClientNetworkTransform : NetworkTransform
{
    /// <summary>
    /// Decide el modo de autoridad del `NetworkTransform`.
    /// <returns>
    /// <c>true</c> si el servidor es autoritativo; <c>false</c> si el owner es autoritativo.
    /// </returns>
    /// <remarks>
    /// En NGO, esto afecta quién puede escribir estado de transform y cómo se reconcilia en clientes.
    /// </remarks>
    protected override bool OnIsServerAuthoritative()
    {
        // Jugadores humanos: authority del owner (cliente) para movimiento local.
        // Bots (FSM/NavMeshAgent): authority del servidor, porque el movimiento se simula en el servidor
        // y debe replicarse a todos los clientes de forma consistente.
        return GetComponent<FSM>() != null;
    }
}
