using UnityEngine;
using UnityEngine.Animations;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using Unity.Netcode; 

namespace Unity.FPS.Gameplay
{
    /// <summary>
    /// Sincroniza visualmente (tercera persona) qué arma tiene equipada un jugador, y ajusta IK de mano izquierda.
    /// <para>
    /// Implementación:
    /// - El owner escucha el cambio real de arma desde <see cref="PlayerWeaponsManager.OnSwitchedToWeapon"/>.
    /// - Publica un índice (<see cref="NetworkedWeaponIndex"/>) que todos leen.
    /// - Cada cliente activa/desactiva un "fake weapon" (modelo 3ª persona) y enlaza el grip a <see cref="LeftHandIKTarget"/>.
    /// </para>
    /// <para>
    /// Red:
    /// - <see cref="NetworkedWeaponIndex"/> es Owner-write: el owner declara qué arma lleva.
    /// - Esto es principalmente visual; no otorga autoridad sobre el arma real.
    /// </para>
    /// <para>
    /// DEFECTUOSO (mapeo por nombre): el emparejamiento se hace por string <see cref="WeaponLink.WeaponName"/>.
    /// Si hay nombres duplicados o cambian, se desincroniza el modelo 3ª persona. Un mapeo por id estable o por
    /// índice real del inventario sería más robusto.
    /// </para>
    /// </summary>
    // ¡NUEVO! Ahora heredamos de NetworkBehaviour
    public class ThirdPersonWeaponSync : NetworkBehaviour
    {
        [System.Serializable]
        /// <summary>
        /// Configuración de enlace entre un arma real (por nombre) y su representación de 3ª persona.
        /// </summary>
        public class WeaponLink
        {
            /// <summary>Nombre lógico del arma real (debe coincidir con <see cref="WeaponController.WeaponName"/>).</summary>
            public string WeaponName;
            /// <summary>GameObject del modelo falso (3ª persona) que se activa cuando este arma está equipada.</summary>
            public GameObject FakeWeapon;
            /// <summary>Nombre del hijo del fake weapon que actúa como punto de agarre para la mano izquierda.</summary>
            public string GripName = "Grip_Left";
        }

        /// <summary>
        /// Gestor de armas del jugador (solo necesario en el owner para detectar cambios).
        /// </summary>
        public PlayerWeaponsManager WeaponsManager;

        [Header("Lista de Armas Falsas")]
        /// <summary>Mapa de armas fake disponibles (índice usado en <see cref="NetworkedWeaponIndex"/>).</summary>
        public WeaponLink[] ThirdPersonWeapons;

        [Header("IK de Tercera Persona")]
        /// <summary>Target (transform) de IK para la mano izquierda.</summary>
        public Transform LeftHandIKTarget;

        // Esta variable se sincroniza sola por internet
        /// <summary>
        /// Índice en <see cref="ThirdPersonWeapons"/> del arma activa (replicado a todos).
        /// </summary>
        public NetworkVariable<int> NetworkedWeaponIndex = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner // Solo el dueño del muñeco puede cambiar su arma
        );

        /// <summary>
        /// Inicializa constraints/handlers y fuerza actualización visual al spawnear en red.
        /// </summary>
        public override void OnNetworkSpawn()
        {
            SetupConstraint(LeftHandIKTarget);

            // Nos suscribimos para escuchar cuando internet nos dice que el jugador cambió de arma
            NetworkedWeaponIndex.OnValueChanged += OnNetworkedWeaponChanged;

            // Si somos el dueño de este jugador, escuchamos al ratón/teclado
            if (IsOwner && WeaponsManager != null)
            {
                WeaponsManager.OnSwitchedToWeapon += HandleLocalWeaponSwitch;
            }

            // Forzamos la actualización visual la primera vez que aparecemos en el mapa
            UpdateVisuals(NetworkedWeaponIndex.Value);
        }

        /// <summary>Desuscribe handlers para evitar fugas.</summary>
        public override void OnNetworkDespawn()
        {
            NetworkedWeaponIndex.OnValueChanged -= OnNetworkedWeaponChanged;

            if (IsOwner && WeaponsManager != null)
            {
                WeaponsManager.OnSwitchedToWeapon -= HandleLocalWeaponSwitch;
            }
        }

        // Esta función solo se ejecuta en TU ordenador cuando tocas la rueda del ratón
        /// <summary>
        /// Handler owner-only: cuando el arma real cambia, publica el índice correspondiente en red.
        /// </summary>
        void HandleLocalWeaponSwitch(WeaponController newWeapon)
        {
            if (newWeapon == null) return;

            // Buscamos qué número de la lista corresponde a tu nueva arma
            for (int i = 0; i < ThirdPersonWeapons.Length; i++)
            {
                if (ThirdPersonWeapons[i].WeaponName == newWeapon.WeaponName)
                {
                    // Al cambiar esta variable, Unity avisa a todos los demás jugadores por internet
                    NetworkedWeaponIndex.Value = i;
                    break;
                }
            }
        }

        // Esta función se ejecuta en TODOS los ordenadores cuando la variable de red cambia
        /// <summary>Callback cuando cambia el índice replicado: actualiza visuales.</summary>
        void OnNetworkedWeaponChanged(int previousValue, int newValue)
        {
            UpdateVisuals(newValue);
        }

        // La función que enciende/apaga los gráficos (igual que la de antes, pero recibe un número)
        /// <summary>
        /// Activa el fake weapon correspondiente al índice y configura IK.
        /// </summary>
        void UpdateVisuals(int index)
        {
            // 1. Apagamos todas
            foreach (var link in ThirdPersonWeapons)
            {
                if (link.FakeWeapon != null) link.FakeWeapon.SetActive(false);
            }

            // 2. Encendemos la que toca
            if (index >= 0 && index < ThirdPersonWeapons.Length)
            {
                var activeLink = ThirdPersonWeapons[index];
                if (activeLink.FakeWeapon != null)
                {
                    activeLink.FakeWeapon.SetActive(true);

                    // 3. Pegamos la mano izquierda
                    if (LeftHandIKTarget != null)
                    {
                        Transform gripLeft = activeLink.FakeWeapon.transform.Find(activeLink.GripName);
                        if (gripLeft != null)
                        {
                            LinkGhost(LeftHandIKTarget, gripLeft);
                        }
                        else
                        {
                            ParentConstraint constraint = LeftHandIKTarget.GetComponent<ParentConstraint>();
                            if (constraint != null) constraint.constraintActive = false;
                        }
                    }
                }
            }
        }

        void SetupConstraint(Transform target)
        {
            // Garantiza que exista ParentConstraint para poder enlazar el grip en runtime.
            if (target == null) return;
            ParentConstraint constraint = target.GetComponent<ParentConstraint>();
            if (constraint == null)
            {
                constraint = target.gameObject.AddComponent<ParentConstraint>();
            }
        }

        void LinkGhost(Transform target, Transform grip)
        {
            // Sustituye la fuente del constraint por el grip y lo activa.
            if (target == null || grip == null) return;
            ParentConstraint constraint = target.GetComponent<ParentConstraint>();

            if (constraint.sourceCount > 0) constraint.RemoveSource(0);

            ConstraintSource newSource = new ConstraintSource();
            newSource.sourceTransform = grip;
            newSource.weight = 1f;

            constraint.AddSource(newSource);
            constraint.SetTranslationOffset(0, Vector3.zero);
            constraint.SetRotationOffset(0, Vector3.zero);
            constraint.constraintActive = true;
        }
    }
}