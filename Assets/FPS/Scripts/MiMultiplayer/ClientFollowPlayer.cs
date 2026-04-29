using Unity.FPS.Game;
using UnityEngine;
using Unity.Netcode;
using Unity.FPS.Gameplay;
using Unity.FPS.UI;


namespace Unity.FPS.AI
{
    /// <summary>
    /// Componente de seguimiento de la posición del player (con offset inicial).
    /// <para>
    /// Uso típico: hacer que un objeto auxiliar (p. ej. UI worldspace, trigger, sensor) siga al jugador.
    /// </para>
    /// <para>
    /// DEFECTUOSO (multiplayer): <see cref="TryResolvePlayerAndOffset"/> usa <see cref="ActorsManager.Player"/>,
    /// que en el FPS sample suele representar el jugador local "principal". En un contexto con múltiples
    /// jugadores/bots, esto puede no ser el objetivo correcto para todas las instancias.
    /// Además, <see cref="LateUpdate"/> no está gated por <see cref="NetworkBehaviour.IsOwner"/>, así que
    /// cualquier cliente podría mover su instancia local basándose en su propio ActorsManager, produciendo
    /// divergencias visuales.
    /// </para>
    /// </summary>
    public class ClientFollowPlayer : NetworkBehaviour
    {

        /// <summary>Transform objetivo que se seguirá (resuelto perezosamente).</summary>
        Transform m_PlayerTransform;
        /// <summary>Offset calculado al resolver el objetivo (mantiene la separación original).</summary>
        Vector3 m_OriginalOffset;

        /// <summary>Primer intento de resolución al iniciar.</summary>
        void Start()
        {
            TryResolvePlayerAndOffset();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsOwner)
            {
                TryResolvePlayerAndOffset();
            }
        }



        void LateUpdate()
        {
            // Nota: no se comprueba IsOwner. Ver observación DEFECTUOSO en cabecera de clase.
            if (m_PlayerTransform == null)
            {
                // En multiplayer el player puede no existir aún cuando spawnea este objeto.
                if (!TryResolvePlayerAndOffset())
                    return;
            }

            transform.position = m_PlayerTransform.position + m_OriginalOffset;
        }

        bool TryResolvePlayerAndOffset()
        {
            // Resolve del player vía ActorsManager (single-player oriented).
            ActorsManager actorsManager = FindAnyObjectByType<ActorsManager>();
            if (actorsManager == null || actorsManager.Player == null)
                return false;

            m_PlayerTransform = actorsManager.Player.transform;
            if (m_PlayerTransform == null)
                return false;

            m_OriginalOffset = transform.position - m_PlayerTransform.position;
            return true;
        }
    }
}
