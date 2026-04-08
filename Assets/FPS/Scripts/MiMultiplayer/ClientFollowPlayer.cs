using Unity.FPS.Game;
using UnityEngine;
using Unity.Netcode;
using Unity.FPS.Gameplay;
using Unity.FPS.UI;


namespace Unity.FPS.AI
{
    public class ClientFollowPlayer : NetworkBehaviour
    {


        Transform m_PlayerTransform;
        Vector3 m_OriginalOffset;

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
