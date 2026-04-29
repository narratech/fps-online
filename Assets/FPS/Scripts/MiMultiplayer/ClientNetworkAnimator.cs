using UnityEngine;
using Unity.Netcode.Components;

/// <summary>
/// `NetworkAnimator` configurado como <b>owner-authoritative</b>.
/// <para>
/// Motivación típica en multijugador:
/// - Los parámetros de animación (correr, disparar, recargar) se activan por input local del owner.
/// - Se replican al resto para que vean la animación sin esperar a round-trips al servidor.
/// </para>
/// <para>
/// Riesgo: si hay clientes no confiables, un owner podría "mentir" sobre animaciones/estados.
/// En este proyecto se usa principalmente para coherencia visual; la jugabilidad autoritativa
/// (daño/respawn/pickups) debe seguir siendo del servidor.
/// </para>
/// </summary>
public class ClientNetworkAnimator : NetworkAnimator
{
    /// <summary>
    /// Indica que este `NetworkAnimator` no es server-authoritative.
    /// </summary>
    protected override bool OnIsServerAuthoritative()
    {
        // false => el owner envía las actualizaciones de animación.
        return false;
    }
}
