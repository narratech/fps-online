# FPS Online

Se convierte el proyecto Unity original UPV-FPS de la versión 6000.3.9.f1 a 6000.3.12f1, perdiendo registros del GitLab anterior.

Se ha borrado la plantilla de documentación que venía originalmente con el repositorio, y a continuación se expone una información mínima.

## Información básica

Human_Prefab representa al jugador humano

Lo más relevante que puede encontrarse en la raíz del prefab:

* NetworkObject: identidad de red del jugador.
* PlayerInput (Input System): componente de Unity que gestiona dispositivos/mapas.
* NewMonoBehaviourScript (tu “ClientPlayerMove” real, el hombre está algo cambiado): habilita cámara/controles solo para owner, crea el HUD del marcador, etc.
* PlayerRespawner: maneja muerte/respawn en red (RPC al server y respawn al cliente).
* ClientNetworkTransform: sincroniza transform (owner authority en tu setup).
* PlayerHealthSync: sincroniza vida/estado.
* PlayerVotingSync (solo en Human): sistema de votación/acciones especiales.
* PlayerNameTag: nombre/kills/deaths en red.
* ClientNetworkAnimator: animación sincronizada.
* Rigging / IK / Weapon sync: WeaponIKSync, ThirdPersonWeaponSync, RigBuilder, constraints, etc.
* UI (CanvasScaler, GraphicRaycaster, TMP): el canvas world-space del nametag y elementos.
