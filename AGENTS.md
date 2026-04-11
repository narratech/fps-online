<!-- UNITY CODE ASSIST INSTRUCTIONS START -->
- Project name: upv-fps
- Unity version: Unity 6000.3.8f1
- Active game object:
  - Name: Pickup_Flamethrower
  - Tag: Untagged
  - Layer: Default
<!-- UNITY CODE ASSIST INSTRUCTIONS END -->

## Netcode — registro de prefabs (NGO)

El `NetworkManager` combina **varias** listas (`NetworkPrefabsLists` en la escena, p. ej. `DefaultNetworkPrefabs.asset` **y** `UniversidadesPrefabsList.asset`). Cada `NetworkObject` prefab debe aparecer **como mucho una vez** en el conjunto fusionado; si el mismo prefab está en dos listas, Unity muestra *duplicate GlobalObjectIdHash*.

Los prefabs de jugador seleccionables (`Human_Prefab`, `UPV_Bot`, `UCM_Bot`, etc.) deben registrarse **solo** en `Assets/UniversidadesPrefabsList.asset`. **No** los añadas a `Assets/DefaultNetworkPrefabs.asset` (ni duplicados en otra lista del mismo `NetworkManager`).

Si el error *duplicate GlobalObjectIdHash* vuelve tras tocar el proyecto en Unity: abre `DefaultNetworkPrefabs.asset` y elimina cualquier entrada cuyo prefab sea uno de esos tres; el inspector de Netcode a veces vuelve a registrar prefabs globales automáticamente.