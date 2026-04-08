using UnityEngine;

namespace Unity.FPS.Game
{
    public class Damageable : MonoBehaviour
    {
        [Tooltip("Multiplier to apply to the received damage")]
        public float DamageMultiplier = 1f;

        [Range(0, 1)]
        [Tooltip("Multiplier to apply to self damage")]
        public float SensibilityToSelfdamage = 0.5f;

        public Health Health { get; private set; }

        void Awake()
        {
            Health = GetComponent<Health>();
            if (!Health)
            {
                Health = GetComponentInParent<Health>();
            }
        }

        public void InflictDamage(float damage, bool isExplosionDamage, GameObject damageSource)
        {
            if (Health)
            {
                var totalDamage = damage;

                if (!isExplosionDamage)
                {
                    totalDamage *= DamageMultiplier;
                }

                if (Health.gameObject == damageSource)
                {
                    totalDamage *= SensibilityToSelfdamage;
                }

                // --- CAMBIO PARA LA RED ---
                // Buscamos nuestro script de red solo por su nombre (para evitar errores de Assembly)
                Component syncScript = Health.GetComponent("PlayerHealthSync");

                if (syncScript != null)
                {
                    // Si es un jugador, enviamos el recado al script de red usando SendMessage
                    // Incluimos también quién hizo el daño para poder contar kills correctamente.
                    Health.SendMessage(
                        "OnNetworkDamageRequested",
                        new object[] { totalDamage, damageSource },
                        SendMessageOptions.DontRequireReceiver
                    );
                }
                else
                {
                    // Si es el bot enemigo (que no tiene script de red), le bajamos la vida directamente
                    Health.TakeDamage(totalDamage, damageSource);
                }
            }
        }
    }
}