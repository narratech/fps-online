# FPS Online

Se convierte el proyecto Unity original UPV-FPS de la versión 6000.3.9.f1 a *6000.3.12f1*, perdiendo registros del GitLab anterior. ¡Ojo, es importante *trabajar todos en esta versión*, para tener la versión funcional con las herramientas de pruebas de modo multijugador de Unity! Recuerda descargarlo usando Git LFS.

Se ha borrado la plantilla de documentación que venía originalmente con el repositorio, y a continuación se expone una información mínima. De todas formas son los ficheros FSM.cs y BotGameplayActions.cs sobre los que hay que trabajar más para cambiarlos por completo y tener allí tanto la máquina de estados jerárquica (capaz de cargar datos de una FSM particular de un fichero de texto y ejecutarla después) como el gestor de acciones con el que CONCRETAMOS lo que se hace o consulta en cada estado o transición de la FSM.

Para poder hacer pruebas de multijugador desde Unity, que es mucho más cómodo que andar creando ejecutables todo el rato, hay que ir a Window > Multiplayer > Multiplayer Play Mode y marcar que queréis al menos un virtual player (Player 2). Se os abrirá una segunda ventana de juego y al dar a Play podréis jugar simultáneamente con las dos ventanas.

## FPS Microgame

Asumimos que la UPV ha empezado trabajando sobre la plantilla de aprendizaje [FPS Microgame](https://learn.unity.com/project/fps-template), de la propia Unity Technologies. Al menos en su última versión esta plantilla es un pequeño juego de disparos en primera persona para un jugador con escenas de victoria/derrota, objetivos, enemigos móviles (hover bots) y torretas, armas variadas y hasta un tutorial embebido (porque está pensado como material para aprender a desarrollar en Unity).

### Descripción

Se trata de una experiencia de acción en primera persona, pensada como plantilla jugable y didáctica: un nivel cerrado donde avanzas, combates y cumples metas hasta ganar o perder. El propósito es ofrecer un bucle de juego completo (moverse, disparar, recoger cosas, completar objetivos, pantalla final, etc.) que sirva de base para aprender o para construir encima un FPS propio.

Cuando juegas apareces en un escenario 3D en vista en primera persona. Tu personaje recorre el entorno, esquiva o enfrenta enemigos y progresa cumpliendo condiciones de misión (por ejemplo las misiones podrían ser eliminar enemigos, llegar a un cierto punto o interactuar con un objeto clave). Si tu salud cae a cero, la partida termina en derrota; si completas todo lo que la misión exige, ganas y pasas a una pantalla de victoria.

Las mecánicas principales son:
* Desplazamiento: caminar en el suelo, correr más rápido, agacharse (ocupas menos, aunque el movimiento supuestamente es más lento), saltar y moverse en el aire mientras saltas.
* Combate: varias armas con comportamientos distintos (disparo simple, ráfaga o disparo que se carga antes de soltar); apuntar suele acercar la “vista“ y estabilizar la puntería; hay munición y recarga. Algunas armas pueden sobrecalentarse visual y sonoramente si se abusa del disparo, tardando más en recuperarse y estar listas de nuevo.
* Daño y supervivencia: recibes daño de enemigos, explosiones de área o caídas muy bruscas (mueres si caes al vacío); puedes curarte con objetos del escenario que son una especie de “hologramas de botiquines“.
* Progresión en el mapa: objetivos que se muestran en pantalla (texto, contadores, avisos cuando queda poco por hacer); a veces un indicador de brújula hacia puntos importantes.
* Jetpack (sólo funciona si está desbloqueado en la partida porque cambia mucho la manera de afrontar los niveles del jugador): en el aire, tras un salto, puedes impulsarte hacia arriba mientras dure el combustible; en suelo o tras un tiempo sin usarlo, el medidor se recupera.
* Objetos que se pueden recoger: salud, munición, armas nuevas, desbloqueo del jetpack, etc., básicamente estos objetos suelen flotar y girar en el nivel hasta que los recoges.
* Fin de partida: transición con oscurecimiento gradual y sonido; según el resultado haya sido favorable o no cargas una pantalla de victoria o de derrota.

Son controles válidos tanto el teclado y ratón, como el mando de juego: 
* Movimiento: direcciones adelante/atrás/a los lados (teclado tipo WASD o flechas; stick izquierdo en el caso del mando).
* Mirar: ratón o stick derecho.
* Saltar
* Sprint (correr)
* Agacharse
* Disparar y apuntar (botones típicos de ratón o gatillos del mando)
* Recargar (en la configuración típica basta con esperar, salvo en armas que tengan la recarga automática desactivada y haya que recargar a través de un botón).
* Cambiar de arma (rueda del ratón y/o ejes en teclado/mando según la asignación de controles del proyecto).
* Menú en partida: está bastante detallado porque ofrece pausa, ajustes de sensibilidad, sombras, hacerte invencible, FPS en pantalla, imagen de controles.
* Jetpack: en el aire, mantener la acción de salto mientras el sistema lo permite y haya combustible (tras haber pulsado salto otra vez en el aire para “armar” el uso).

### Clases y sus relaciones

Los sistemas principales (bloques en que estructura la aplicación) que encontramos en el diseño software de este juego son estos:

| Sistema             | Rol |
|--------------------|-----|
| Flujo de partida   | Decide cuándo termina la sesión (victoria por objetivos, derrota por muerte del jugador), atenúa audio, muestra mensajes y carga la escena final. |
| Objetivos          | Definen qué debe ocurrir para “ganar”; el gestor comprueba que no quede ningún objetivo obligatorio pendiente y dispara el evento de victoria. |
| Eventos            | Canal común para avisar a HUD, flujo de juego, objetivos y feedback sin acoplar cada script al resto. |
| Personaje          | Entrada del jugador, movimiento/física del avatar, armas activas, jetpack y registro como “actor” del mundo. |
| Combate y daño     | Armas lanzan proyectiles o efectos; impactos buscan componentes de daño/vida; hay multiplicadores y zonas de explosión. |
| IA enemiga         | Detección por visión, rangos de ataque, patrullas o torreta fija; comparte el mismo modelo de vida/daño/armas que el jugador a nivel conceptual. |
| Escenario y reglas | Actores registrados, capas de ignorar ciertos rayos, objetos destruibles, teleports, etc. |
| Interfaz           | Vida, munición, armas, brújula, objetivos, notificaciones, menú, contadores. |
| Audio y calidad    | Mezclas, utilidades de sonido, opciones de menú, herramientas de editor/profiler. |

Lamentablemente hay muy pocos comentarios y muy breves en el código. El típico flujo conceptual sería: entrada → personaje → mundo (colisiones, objetos que se pueden recoger) → IA y armas → vida/daño → eventos → UI y flujo de partida.

Los scripts con código están agrupados por carpetas. Hay scripts de NavMeshComponents, que es la biblioteca estándar de navegación en malla que ofrece Unity; y luego hay Tutoriales / PublishCriteria, que son criterios del flujo tutorial/publicación, que no tienen nada que ver con este juego de acción. 
El núcleo y las utilidades del juego están en: Assets/FPS/Scripts/Game/ 

En este diagrama se muestran las clases principales.

```mermaid
classDiagram
    direction TB

    class EventManager {
        <<static>>
        +AddListener()
        +Broadcast()
        +RemoveListener()
    }

    class GameFlowManager {
        +EndGame()
    }

    class ObjectiveManager {
        +Update loop objectives
    }

    class ActorsManager {
        +Actors
        +Player
    }

    class Actor {
        +Affiliation
        +AimPoint
    }

    class Health {
        +TakeDamage()
        +Heal()
    }

    class Damageable {
        +InflictDamage()
    }

    class PlayerCharacterController {
        +movement, jump, fall damage
    }

    class PlayerInputHandler {
        +GetMoveInput()
        +GetFireInput()
    }

    class PlayerWeaponsManager {
        +SwitchWeapon
        +Aiming FOV
    }

    class Jetpack {
        +fuel, thrust
    }

    class WeaponController {
        +ShootType
        +ammo, charge
    }

    class ProjectileBase {
        +Shoot()
    }

    class ProjectileStandard {
        +hit, damage
    }

    class DamageArea {
        +InflictDamageInArea()
    }

    class EnemyController {
        +patrol, weapons, death
    }

    class DetectionModule {
        +HandleTargetDetection()
    }

    class NavigationModule {
        +Nav tuning
    }

    class EnemyMobile
    class EnemyTurret

    class EnemyManager {
        +RegisterEnemy
    }

    class Objective {
        <<abstract>>
        +CompleteObjective()
    }

    class ObjectiveKillEnemies
    class ObjectiveReachPoint
    class ObjectivePickupItem

    class Pickup {
        +OnPicked()
    }

    GameFlowManager ..> EventManager : escucha
    ObjectiveManager ..> Objective : lista
    ObjectiveManager ..> EventManager : victoria
    PlayerCharacterController --> PlayerInputHandler
    PlayerCharacterController --> PlayerWeaponsManager
    PlayerCharacterController --> Health
    PlayerCharacterController --> Actor
    Jetpack --> PlayerCharacterController
    Jetpack --> PlayerInputHandler
    PlayerWeaponsManager --> PlayerInputHandler
    PlayerWeaponsManager --> WeaponController
    WeaponController --> ProjectileBase
    ProjectileStandard --|> ProjectileBase
    ProjectileStandard --> DamageArea
    Damageable --> Health
    Actor --> ActorsManager : registro
    EnemyController --> Health
    EnemyController --> Actor
    EnemyController --> DetectionModule
    EnemyController --> NavigationModule
    EnemyController --> WeaponController
    EnemyMobile --> EnemyController
    EnemyTurret --> EnemyController
    EnemyController --> EnemyManager
    Objective ..> EventManager : HUD / mensajes
    ObjectiveKillEnemies --|> Objective
    ObjectiveReachPoint --|> Objective
    ObjectivePickupItem --|> Objective
    Pickup ..> EventManager : PickupEvent
    DetectionModule --> ActorsManager
```
He aquí con el nucleo del juego y algunas utilidades, según clases (archivos) del proyecto:

| Clase | Responsabilidad | Problema que resuelve | Interacciones |
|-----------------|----------------|----------------------|---------------|
| EventManager + tipos en Events | Bus de eventos tipados (suscribir / emitir). | Desacoplar sistemas (HUD, objetivos, flujo) sin referencias cruzadas masivas. | Usado casi en todo: muerte, pickups, daño, objetivos, mensajes. |
| GameFlowManager | Fin de partida: fade, escena win/lose, cursor, volumen. | Un solo lugar para “cerrar” la sesión de forma coherente. | Escucha eventos de victoria y muerte; bloquea entrada vía GameIsEnding. |
| ObjectiveManager | Lista dinámica de objetivos y comprobación de bloqueo. | Saber cuándo todos los objetivos obligatorios están cumplidos. | Objetivos se auto-registran al crearse; emite victoria. |
| ActorsManager | Lista de Actor y referencia al jugador. | IA y detección necesitan saber “quién hay en el nivel” y dónde está el jugador. | Actor al iniciar; DetectionModule, FollowPlayer, etc. |
| Actor | Afiliación (equipo) y punto de apuntado. | Evitar que aliados se disparen entre sí y dar un ancla de raycast. | Registro en ActorsManager; comparación de Affiliation en IA. |
| GameConstants | Nombres de ejes/botones legados. | Texto único para referencias antiguas; parte del input real va por otro sistema. | Uso residual en código que aún nombra ejes clásicos. |
| AudioManager | Acceso a mezcladores y parámetros. | Centralizar ajustes de volumen por grupos. | Menú y utilidades de audio. |
| AudioUtility | Crear SFX, grupos, volumen maestro. | Sonidos one-shot sin duplicar lógica en cada pickup/proyectil. | Pickups u objetos que se pueden coger, armas, impactos. |
| DebugUtility | Comprobaciones nulas con mensajes claros. | Fallar rápido en editor con contexto. | Muchos Start/Awake. |
| PrefabReplacer / PrefabReplacerOnInstance (+ Editor) | Sustituir prefabs en escena o instancia. | Herramienta de contenido/migración, no runtime del jugador. | Editor y pipelines de assets. |
| MeshCombiner / MeshCombineUtility | No es un sistema de jugabilidad. Combinar mallas para rendimiento. |  Menos draw calls en escenas estáticas. | Escena / build. |
| TimedSelfDestruct | No es un sistema de jugabilidad. Destruir objeto tras tiempo. | Limpieza de VFX o temporales. | Genérico. |
| ConstantRotation | No es un sistema de jugabilidad. Giro continuo. | Decoración simple. | Props / UI. |
| IgnoreHitDetection / IgnoreHeatMap | Excluir de ciertos chequeos. | Raycasts o mapas de calor no golpean triggers no deseados. | Capas / diseño de nivel. |

EventManager estático: simple y efectivo, pero estado global; hay que suscribirse y desuscribirse en OnDestroy para evitar fugas o callbacks a objetos destruidos (el código del proyecto ya lo hace en los sitios sensibles como objetivos y flujo).

ObjectiveManager + subclases de Objective son clases importantes aquí: la victoria se define como “todos los no opcionales completos”; el orden de registro y el flag IsOptional definen el diseño de la misión.

ActorsManager es un componente que suele vivir una sola vez en la escena (tipo GameManager). Es un registro global ligero de “actores con equipo y punto de mira”, más la referencia explícita al jugador. No mueve personajes ni decide combate por sí solo: habilita que la IA y utilidades encuentren “quiénes son” y “dónde apuntar” sin acoplar cada enemigo al prefab del jugador. Se encarga de dos cosas:
* Actors: lista de todos los Actor que se han registrado. Se inicializa vacía en Awake.
* Player: referencia al GameObject del jugador. No la rellenan los enemigos: la asigna PlayerCharacterController en Awake con actorsManager.SetPlayer(gameObject) para que otros scripts no tengan que buscar al jugador por etiquetas o nombres.

Actor va en cada entidad que cuenta como “personaje” en el sentido de equipos y puntería (tanto el jugador como los enemigos llevan Actor). En Start, busca el ActorsManager de la escena; y si no está ya en la lista, lo añade con Actors.Add(this).
En OnDestroy se elimina de la lista para no dejar referencias colgando. Cada Actor expone un Affiliation o número de “equipo”. La IA usa una regla simple: si el Affiliation es distinto, es hostil. Misma afiliación ⇒ no se tratan como blanco (no entra en el bucle de enemigos del DetectionModule).
El actor también expone un AimPoint: transform al que apuntan los rayos enemigos y que se usa en la línea de visión (“¿veo al otro actor?”). El jugador reposiciona ese punto al agacharse para que la altura del blanco sea coherente con la cápsula.

He aquí más clases con el modelpo de combate, el sistema de salud (o vida) y el de las misiones:

| Clase | Responsabilidad | Problema | Interacciones |
|-------|----------------|----------|---------------|
| Health | Salud máxima, actual, invencibilidad, curación, muerte. | Fuente única de verdad sobre el estado vital. | Damageable, PlayerCharacterController, EnemyController, UI de vida. |
| Damageable | Multiplicadores de daño y daño en cadena hacia Health. | Separar “zona golpeada” del “pool de vida” del personaje. | Proyectiles, explosiones, self-damage reducido. |
| WeaponController | Ciclo de disparo (manual/auto/carga), munición, recarga, spread, VFX/SFX, spawneo de proyectil. | Todas las armas comparten el mismo contrato. | PlayerWeaponsManager, EnemyController, pickups de munición. |
| ProjectileBase | Inicializa dueño, dirección, carga heredada. | Punto común antes del comportamiento concreto del proyectil. | WeaponController llama Shoot(). |
| ProjectileStandard | Movimiento, gravedad, impacto, daño directo o por área. | Balas y proyectiles “físicos” en el nivel. | Damageable, DamageArea, capas. |
| DamageArea | Daño en esfera con curva por distancia. | Explosiones sin dañar dos veces al mismo Health. | Lanzadores / explosivos. |
| Destructable | Al morir el Health, destruye el GameObject. | Props rompibles mínimos. | Niveles con cajas/estructuras. |
| Objective (abstracta) | Título, descripción, opcional, completar y notificar. | Plantilla para cualquier tipo de misión. | ObjectiveManager, eventos de UI. |
| MinMaxParameters (si existe como struct) | Rangos numéricos reutilizables. | Evitar magic numbers sueltos en IA/VFX. | Está referenciado en varios sitios. |

WeaponController + ProjectileStandard + DamageArea son clases también muy importantes: cadena completa de daño desde el input hasta área de explosión; hay muchos casos (spread, carga, recarga automática, física de casquillo).

Muchos comportamientos son configurables aquí, por ejemplo: si AutomaticReload está a cierto (valor por defecto en WeaponController) no hace falta pulsar recargar para que ese arma se recargue. Tras dejar de disparar, pasado un AmmoReloadDelay (por defecto 2 s), la munición vuelve sola poco a poco según AmmoReloadRate mientras haya hueco por debajo de MaxAmmo. Por otro lado si AutomaticReload está a falso, ahí sí entra el botón de recargar (en el asset de entrada está ligado a R en teclado y al botón de la izquierda del mando, entre otras asignaciones). El gestor de armas del jugador comprueba GetReloadButtonDown() y llama a StartReloadAnimation() si aún no tienes el cargador “lleno” a nivel de ratio.

Más clases relacionadas con el jugador, las armas, los pickups u objetos a recoger, los objetivos concretos:

| Clase | Responsabilidad | Problema | Interacciones |
|-------|----------------|----------|---------------|
| PlayerCharacterController | Movimiento con controlador de personaje, salto, sprint, agachado, cámara vertical, sonidos de paso, daño por caída, altura de muerte. | Sensación FPS clásica en un solo componente (núcleo crítico por acoplamiento con input, armas y vida). | PlayerInputHandler, PlayerWeaponsManager, Health, Jetpack, escena. |
| PlayerInputHandler | Lee acciones, sensibilidad, bloqueo si menú/fin de juego. | Una sola puerta para saber cuando “¿el jugador puede actuar?”. | Todas las lecturas de disparo/mira/mover. |
| PlayerWeaponsManager | Slots de armas, cambio animado, apuntado, FOV, bob/recoil, detección de enemigo en mira. | Capa de presentación y estado de armas encima de WeaponController. | WeaponController, cámara, capas enemigo. |
| Jetpack | Combustible, empuje vertical, recarga, desbloqueo. | Movilidad vertical sin romper el salto normal (regla: segundo salto en aire habilita uso + mantener salto). | PlayerCharacterController, PlayerInputHandler, HUD. |
| JetpackPickup | Desbloqueo al recoger. | Progresión de habilidad en el nivel. | Jetpack.TryUnlock(), Pickup. |
| Pickup (base) | Bobina/gira, trigger, evento genérico de recogida. | Comportamiento común de ítems. | Subclases y ObjectivePickupItem (evento). |
| HealthPickup, AmmoPickup, WeaponPickup | Aplicar curación, munición o nueva arma al recoger. | Especialización mínima del pickup. | Health, WeaponController, PlayerWeaponsManager. |
| TeleportPlayer | Mover al jugador a otro punto. | Atajos de diseño / secretos. | PlayerCharacterController / transform. |
| PositionBobbing | Oscilación de transform. | Animación barata de objetos. | Props. |
| ChargedWeaponEffectsHandler / ChargedProjectileEffectsHandler | Feedback visual/sonoro ligado a carga. | Armas cargadas legibles para el jugador. | WeaponController / proyectiles. |
| WeaponFuelCellHandler | Lógica de “celdas”/combustible de arma si aplica al prefab. | Variante de munición no bala a bala. | Armas concretas. |
| OverheatBehavior | Steam, gradientes, sonido al enfriar según ratio de munición/calor. | Feedback de sobrecalentamiento acoplado al arma. | WeaponController. |
| ObjectiveKillEnemies | Progreso por bajas (todos o N). | Misión tipo “limpiar zona”. | EnemyKillEvent. |
| ObjectiveReachPoint | Trigger al entrar el jugador. | Misión de extracción o checkpoint. | PlayerCharacterController, destruye marcador. |
| ObjectivePickupItem | Completar al recoger un objeto concreto (incluso sin efecto de pickup). | Misión de “llave” o coleccionable. | PickupEvent. |

PlayerCharacterController es una clase muy importante: concentra movimiento, cámara, sonidos, caídas, muerte y coordinación con armas y eventos; cualquier cambio de “cómo se siente el juego” pasa por aquí.

Y por último estas son las clases relativas a la IA de los enemigos:

| Clase | Responsabilidad | Problema | Interacciones |
|-------|----------------|----------|---------------|
| EnemyController | Orquestación: navegación, patrulla, armas del enemigo, daño visual, ojos, loot, registro. | Cerebro compartido entre móvil y torreta (componente grande y central de la IA). | NavMeshAgent, DetectionModule, Health, WeaponController, EnemyManager, PatrolPath. |
| EnemyMobile | Máquina de estados que tiene patrulla / seguir / atacar y animación. | Comportamiento del hoverbot. | EnemyController. |
| EnemyTurret | Estados idle/ataque, pivotes de apuntado, delays de fuego. | Enemigo estático que gira y dispara. | EnemyController, Health. |
| DetectionModule | Raycasts a actores hostiles, rangos ver/ataque, timeout de memoria. | “Ver” sin mirar a través de paredes (raycast ordenado por hit). | ActorsManager, Actor, colliders propios ignorados. |
| NavigationModule | Parámetros de agente (velocidad, aceleración) para moverse. | Tunear NavMesh sin tocar prefabs a mano en cada sitio. | EnemyController + NavMeshAgent. |
| PatrolPath | Nodos de ruta. | Patrullas diseñables en editor. | EnemyController. |
| EnemyManager | Lista viva de enemigos y broadcast al matar. | Conteo global para objetivos y UI. | EnemyController al registrar/desregistrar. |
| FollowPlayer | Sigue al jugador conservando offset. | Cámara u objeto ligado al jugador. | ActorsManager.Player. |

EnemyController + DetectionModule son clases importantes aquí: EnemyController tiene su propio Actor y pasa m_Actor a la detección. La IA depende de la lista global de actores y de raycasts bien filtrados; errores de capas o de Affiliation rompen combate o hacen injusto el juego.
DetectionModule es el que recorre m_ActorsManager.Actors, filtra por afiliación distinta, mide distancia y lanza rayos hacia otherActor.AimPoint para ver si hay línea de visión.

FollowPlayer usa ActorsManager.Player para seguir al transform del jugador.

En cuanto a interfaz de usuario podríamos añadir que la mayoría son componentes pequeños de presentación que escuchan el mismo bus de eventos o leen estado del jugador/armas:
* HUD combate: WeaponHUDManager, AmmoCounter, CrosshairManager, PlayerHealthBar, FeedbackFlashHUD, StanceHUD, JetpackCounter, EnemyCounter.
* Misión y mundo: ObjectiveHUDManager, ObjectiveToast, NotificationHUDManager, NotificationToast, Compass + CompassElement / CompassMarker.
* Menús y utilidades: InGameMenuManager, MenuNavigation, LoadSceneButton, ToggleGameObjectButton, DisplayMessage / DisplayMessageManager.
* Tablas y layout: UITable (+ UITableEditor).
* Varios: FramerateCounter, TakeScreenshot, FillBarColorChange, WorldspaceHealthBar.
El problema común que resuelven estos componentes es mantener la interfaz sincronizada con eventos (ObjectiveUpdateEvent, EnemyKillEvent, etc.), sin conocer la implementación interna de armas u objetivos.

En la subcarpeta especial Editor/ hay herramientas de autor (PrefabReplacerEditor -para migraciones masivas de prefabs-, MiniProfiler -para analizar el rendimiento de la escena-, ShaderBuildStripping -para reducir tiempo de compilación de shaders-), que en realidad no participan en el build de jugador salvo que se incluyan; están pensadas para optimizar el flujo de trabajo y compilación.

En la carpeta Tutorials/ está TutorialCallbacks y criterios de publicación: está diseñado para integrarse con el flujo de tutorial de Unity Learn, pero vamos, no tiene nada que ver con la jugabilidad.

## FPS UPV

La versión multijugador ha sido desarrollada por el equipo de la UPV y se llama FPS-UPV, cuyo ZIP puede descargarse del [sitio web de la competición Bot Prize](https://botprize2026.ai2.upv.es/) del congreso [Conference on Games 2026](https://cog2026.fdi.ucm.es/).

### Descripción

### Clases y sus relaciones

*Human_Prefab* representa al jugador humano y *UCM_Bot* es la IA que hay que programar si se quiere tener un bot contra el que enfrentarse.

#### Human_Prefab
Ruta: Assets/FPS/Scripts/MiMultiplayer/Human_Prefab.prefab

Es el “paquete completo” del jugador: control FPS, cámara, armas, vida/daño y los componentes oficiales de Netcode que permiten hacer multijugador en Unity.

Lo más relevante que puede encontrarse en la raíz de este prefab es esto:

* NetworkObject: identidad de red del jugador.
* PlayerInput (Input System): componente de Unity que gestiona dispositivos/mapas de entrada.
* NewMonoBehaviourScript (tu “ClientPlayerMove” real, el hombre es que no está bien puesto): habilita cámara/controles sólo para el propietario, crea el HUD del marcador, etc.
* PlayerRespawner: maneja muerte/respawn en red (RPC al server y respawn al cliente).
* ClientNetworkTransform: sincroniza transform (owner authority en tu setup).
* PlayerHealthSync: sincroniza vida/estado.
* PlayerVotingSync (solo en Human): sistema de votación/acciones especiales.
* PlayerNameTag: nombre/kills/deaths en red.
* ClientNetworkAnimator: Script para hacer animación sincronizada.
* Rigging / IK / Weapon sync: WeaponIKSync, ThirdPersonWeaponSync, RigBuilder, constraints, etc. Son scripts de sincronización (por ejemplo PlayerHealthSync, ThirdPersonWeaponSync, LocalVisibility...).
* UI (CanvasScaler, GraphicRaycaster, TMP): el canvas world-space del nametag y elementos.
* CharacterController: componente nativo de Unity para mover un “personaje tipo cápsula” en el mundo sin usar un Rigidbody. Gestiones colisiones, deslizamiento, movimiento 'cinemático', grounding básico... pero no hace nada más.
* PlayerCharacterController: Script de este proyecto que hace las veces de MENTE del CharacterController, lee la entrada con PlayerInputHandler, y lo convierte en movimiento, rotación, coordina la cámara, la animación, está pendiente de la salud, muerte, apuntado, etc. 

#### UCM_Bot
Ruta: Assets/FPS/Scripts/MiMultiplayer/UCM_Bot.prefab

En UCM_Bot encontramos componentes muy parecidos, aunque se ha añadido FSM como ejemplo de dónde podría ir una máquina de estados que tome las decisiones de ese bot (hay que sustituir COMPLETAMENTE todo ese código), y BotGameplayActions para hacer las veces de gestor de acciones, aunque también hace cosas como crear el componente NavMeshAgent en caso de que no lo tenga (que de hecho no lo tiene añadido ahora mismo).


