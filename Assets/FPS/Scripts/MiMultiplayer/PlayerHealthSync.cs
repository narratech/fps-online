using Unity.Netcode;
using UnityEngine;
using Unity.FPS.Game;

/// <summary>
/// Sincronización de daño para el componente <see cref="Health"/> usando NGO.
/// <para>
/// Flujo:
/// - El cliente que cree haber causado daño emite un evento local (vía <c>SendMessage</c> desde `Damageable`)
///   que acaba aquí en <see cref="OnNetworkDamageRequested"/>.
/// - Si está en host/servidor: aplica daño autoritativamente y replica a clientes.
/// - Si está en cliente puro: solicita al servidor por <see cref="RequestDamageServerRpc"/> y el servidor
///   lo aplica + notifica al resto con <see cref="ApplyDamageClientRpc"/>.
/// </para>
/// <para>
/// <b>DEFECTUOSO (seguridad/autoridad):</b> el servidor aplica el daño tal y como se le pide
/// (cantidad y fuente) sin validar:
/// - que el disparo fuera posible,
/// - que el `damageSource` sea realmente el atacante,
/// - que el objetivo sea el correcto,
/// - que exista línea de visión, cadencia, munición, etc.
/// Con `RequireOwnership = false`, cualquier cliente puede invocar el RPC y potencialmente "matar" a otros.
/// Esto debería endurecerse si el proyecto requiere antitrampas o consistencia estricta.
/// </para>
/// <para>
/// Nota de diseño: el código intenta evitar doble aplicación en host usando <c>if (IsServer) return;</c>
/// dentro del ClientRpc.
/// </para>
/// </summary>
public class PlayerHealthSync : NetworkBehaviour
{
    /// <summary>
    /// Referencia al componente de vida real del FPS Sample que modifica HP, dispara eventos, etc.
    /// </summary>
    Health m_Health;

    void Awake()
    {
        // Cogemos el script de vida original
        m_Health = GetComponent<Health>();
    }

    // Esta función es llamada por Damageable.cs usando SendMessage
    // Nota: usamos object para poder mandar (damage, damageSource) sin acoplar por overloads.
    /// <summary>
    /// Entrada "de alto nivel" para el pipeline de daño: el Damageable local pide aplicar daño en red.
    /// <para>
    /// Firma con <see cref="object"/> por compatibilidad con <c>SendMessage</c>:
    /// - <c>float</c> => solo daño.
    /// - <c>object[]</c> => [0]=daño (<c>float</c>), [1]=fuente (<see cref="GameObject"/>).
    /// </para>
    /// </summary>
    void OnNetworkDamageRequested(object payload)
    {
        // Decodificación defensiva del payload.
        float damage = 0f;
        GameObject damageSource = null;

        if (payload is float f)
        {
            damage = f;
        }
        else if (payload is object[] arr && arr.Length >= 1)
        {
            if (arr[0] is float f0) damage = f0;
            if (arr.Length >= 2) damageSource = arr[1] as GameObject;
        }

        // El ordenador que ha disparado la bala le pide al servidor que aplique el daño.
        // Autoridad: el servidor es quien debe modificar HP y replicar el efecto.
        if (IsServer)
        {
            // Si ya estamos en el servidor (host), aplicamos autoritativamente y notificamos al resto.
            var damageSourceRef = ToNetRefOrNull(damageSource);
            ApplyDamageServer(damage, damageSourceRef);
            ApplyDamageClientRpc(damage, damageSourceRef);
        }
        else
        {
            RequestDamageServerRpc(damage, ToNetRefOrNull(damageSource));
        }
    }

    /// <summary>
    /// RPC cliente→servidor solicitando aplicar daño.
    /// </summary>
    /// <remarks>
    /// DEFECTUOSO: al no requerir ownership y no validar en servidor, es un vector directo de trampas.
    /// </remarks>
    [ServerRpc(RequireOwnership = false)]
    void RequestDamageServerRpc(float damage, NetworkObjectReference damageSourceRef)
    {
        ApplyDamageServer(damage, damageSourceRef);
        ApplyDamageClientRpc(damage, damageSourceRef);
    }

    // El servidor da la orden y esto se ejecuta en las pantallas de TODOS los jugadores
    /// <summary>
    /// RPC servidor→clientes para aplicar el mismo daño en las instancias remotas.
    /// </summary>
    [ClientRpc]
    void ApplyDamageClientRpc(float damage, NetworkObjectReference damageSourceRef)
    {
        // En host, el servidor ya aplicó el daño; evitamos doble aplicación.
        if (IsServer) return;

        if (m_Health != null)
        {
            // Ahora sí, ejecutamos el daño real en el script original de cada ordenador a la vez
            GameObject damageSource = null;
            if (damageSourceRef.TryGet(out NetworkObject no) && no != null)
                damageSource = no.gameObject;

            m_Health.TakeDamage(damage, damageSource);
        }
    }

    /// <summary>
    /// Aplicación autoritativa de daño en el servidor.
    /// </summary>
    void ApplyDamageServer(float damage, NetworkObjectReference damageSourceRef)
    {
        if (!IsServer) return;
        if (m_Health == null) return;

        GameObject damageSource = null;
        if (damageSourceRef.TryGet(out NetworkObject no) && no != null)
            damageSource = no.gameObject;

        m_Health.TakeDamage(damage, damageSource);
    }

    /// <summary>
    /// Convierte un <see cref="GameObject"/> (o alguno de sus padres) a <see cref="NetworkObjectReference"/>.
    /// <para>
    /// Se usa para transportar la fuente de daño sin referencias directas (y para atribución de kills).
    /// </para>
    /// </summary>
    NetworkObjectReference ToNetRefOrNull(GameObject go)
    {
        if (go == null) return default;
        var netObj = go.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            // A veces el damageSource llega como un hijo (arma/brazo/cámara).
            // Para atribuir kills correctamente necesitamos el NetworkObject del root del jugador.
            netObj = go.GetComponentInParent<NetworkObject>();
        }
        if (netObj == null) return default;
        return netObj;
    }
}