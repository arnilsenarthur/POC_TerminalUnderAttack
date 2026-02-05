# Assembly Definition Dependencies Analysis

This document lists all asmdef files needed for each folder in `/Scripts/` and their dependencies.

## Folder Structure

```
Assets/Scripts/
├── Core/              (TUA_Core)
├── Entities/          (TUA_Entities)
├── Items/             (TUA_Items)
├── Misc/              (TUA_Misc)
├── Systems/           (TUA_Systems)
├── UI/                (TUA_UI)
├── Settings/          (TUA_Settings)
├── I18n/              (TUA_I18n)
├── GameModes/         (TUA_GameModes) - Currently no asmdef
└── Serializers_ImplNet.cs (Root level - needs TUA_Serializers or included in TUA_Core)
```

## Dependency Graph

### 1. **TUA_Core** (`Assets/Scripts/Core/`)
**Dependencies:**
- `TUA.Misc` (for NetBehaviour, SingletonNetBehaviour, Uuid, WeaponState, Registry, RegistrableScriptableObject)
- FishNet (for networking)
- Unity Engine

**Used by:**
- TUA_Entities
- TUA_Items
- TUA_Systems
- TUA_UI
- TUA_GameModes
- TUA_Serializers (if created)

**Notes:**
- Core contains fundamental types (Entity, GameWorld, Inventory, ItemStack, Item, GamePlayer, GameMode)
- Has circular reference with Core.Interfaces (IInventoryHolder references Core, but Core uses Interfaces)
  - Solution: Keep Interfaces in Core, no separate asmdef needed

---

### 2. **TUA_Entities** (`Assets/Scripts/Entities/`)
**Dependencies:**
- `TUA.Core` (Entity, GameWorld, Interfaces, Inventory)
- `TUA.Core.Interfaces` (IHealth, IInventoryHolder, IPovHandler, IWeaponUser)
- `TUA.Items` (for item types)
- `TUA.Misc` (NetBehaviour, SingletonNetBehaviour, Uuid, WeaponState)
- `TUA.Settings` (for PlayerEntity settings)
- FishNet (for networking)
- Unity Engine
- Unity Animation Rigging

**Used by:**
- TUA_Systems
- TUA_UI
- TUA_GameModes

**Notes:**
- Contains PlayerEntity, HackingTarget, and their implementations
- Has _ImplNet partial classes that reference Core

---

### 3. **TUA_Items** (`Assets/Scripts/Items/`)
**Dependencies:**
- `TUA.Core` (Item, ItemStack, Registry, RegistrableScriptableObject)
- `TUA.Core.Interfaces` (IInventoryHolder)
- Unity Engine

**Used by:**
- TUA_Entities
- TUA_Systems
- TUA_UI
- TUA_GameModes
- TUA_Serializers (if created)

**Notes:**
- Contains WeaponItem, DataDriveItem, HackerToolItem and their stacks
- Depends on Core for base Item and ItemStack classes

---

### 4. **TUA_Misc** (`Assets/Scripts/Misc/`)
**Dependencies:**
- FishNet (for networking)
- Unity Engine

**Used by:**
- TUA_Core
- TUA_Entities
- TUA_Systems
- TUA_UI

**Notes:**
- Contains utility classes: NetBehaviour, SingletonNetBehaviour, Uuid, WeaponState, Registry, RegistrableScriptableObject
- No dependencies on other TUA namespaces (lowest level)
- Should be referenced by most other assemblies

---

### 5. **TUA_Systems** (`Assets/Scripts/Systems/`)
**Dependencies:**
- `TUA.Core` (GameWorld, Entity, Inventory, Interfaces)
- `TUA.Core.Interfaces` (IInventoryHolder, IWeaponUser)
- `TUA.Entities` (PlayerEntity, HackingTarget)
- `TUA.Items` (WeaponItemStack, DataDriveItemStack, HackerToolItem)
- `TUA.Misc` (SingletonNetBehaviour, NetBehaviour, Uuid, WeaponState)
- FishNet (for networking)
- Unity Engine

**Used by:**
- TUA_UI

**Notes:**
- Contains game systems: WeaponSystem, HackingSystem, FeedSystem, DeliverySystem, CameraSystem
- Has _ImplNet partial classes for networking

---

### 6. **TUA_UI** (`Assets/Scripts/UI/`)
**Dependencies:**
- `TUA.Core` (GameWorld, Entity, Inventory, Interfaces)
- `TUA.Core.Interfaces` (IHealth, IInventoryHolder)
- `TUA.Entities` (PlayerEntity)
- `TUA.Items` (WeaponItemStack, HackerToolItem)
- `TUA.Misc` (SingletonBehaviour, Uuid)
- `TUA.Systems` (WeaponSystem, FeedSystem, HackingSystem)
- `TUA.I18n` (LocalizationManager)
- `TUA.Settings` (for SettingsMenuController)
- FishNet (for networking)
- Unity Engine
- Unity UI Toolkit
- Unity UI (legacy)

**Used by:**
- None (top-level UI layer)

**Notes:**
- Contains all UI controllers and windows
- Has _ImplNet partial classes for networking (RelayLobbyGUI)

---

### 7. **TUA_Settings** (`Assets/Scripts/Settings/`)
**Dependencies:**
- Unity Engine
- Unity UI Toolkit

**Used by:**
- TUA_UI
- TUA_Entities (PlayerEntity uses settings)

**Notes:**
- Contains settings system
- Minimal dependencies, mostly self-contained

---

### 8. **TUA_I18n** (`Assets/Scripts/I18n/`)
**Dependencies:**
- Unity Engine

**Used by:**
- TUA_UI
- TUA_Settings

**Notes:**
- Contains localization system
- No dependencies on other TUA namespaces (lowest level)

---

### 9. **TUA_GameModes** (`Assets/Scripts/GameModes/`)
**Dependencies:**
- `TUA.Core` (GameMode, GameWorld, Inventory)
- `TUA.Entities` (PlayerEntity)
- `TUA.Items` (WeaponItemStack, ItemStack)
- Unity Engine

**Used by:**
- None (used by Core/GameWorld at runtime)

**Notes:**
- Currently no asmdef file exists
- Contains game mode implementations (DefaultGameMode)
- Should be created if GameModes folder grows

---

### 10. **TUA_Serializers** (Root level - `Assets/Scripts/Serializers_ImplNet.cs`)
**Dependencies:**
- `TUA.Core` (ItemStack, Inventory, GamePlayer)
- `TUA.Items` (WeaponItemStack, DataDriveItemStack)
- `TUA.Misc` (Uuid)
- FishNet (for serialization)
- Unity Engine

**Used by:**
- None (used by FishNet at runtime)

**Notes:**
- Currently a single file at root level
- Could be moved to a Serializers folder with its own asmdef
- Or could be included in TUA_Core since it's closely related

---

## Recommended Assembly Definition Files

### Summary Table

| Assembly | Folder | Internal Dependencies | External Dependencies |
|----------|--------|----------------------|----------------------|
| **TUA_Misc** | `Misc/` | None | FishNet, Unity Engine |
| **TUA_I18n** | `I18n/` | None | Unity Engine |
| **TUA_Core** | `Core/` | TUA_Misc | FishNet, Unity Engine |
| **TUA_Items** | `Items/` | TUA_Core | Unity Engine |
| **TUA_Settings** | `Settings/` | None | Unity UI Toolkit |
| **TUA_Entities** | `Entities/` | TUA_Core, TUA_Items, TUA_Misc, TUA_Settings | FishNet, Unity Engine, Unity Animation Rigging |
| **TUA_Systems** | `Systems/` | TUA_Core, TUA_Entities, TUA_Items, TUA_Misc | FishNet, Unity Engine |
| **TUA_UI** | `UI/` | TUA_Core, TUA_Entities, TUA_Items, TUA_Misc, TUA_Systems, TUA_I18n, TUA_Settings | FishNet, Unity Engine, Unity UI Toolkit, Unity UI |
| **TUA_GameModes** | `GameModes/` | TUA_Core, TUA_Entities, TUA_Items | Unity Engine |
| **TUA_Serializers** | Root (`Serializers_ImplNet.cs`) | TUA_Core, TUA_Items, TUA_Misc | FishNet, Unity Engine |

### Detailed Configuration

#### 1. **TUA_Misc** (`Assets/Scripts/Misc/`)
**References:**
- `GUID:2e99c95bbb9ddd5439acfb79b643b82e` (FishNet.Runtime)
- `GUID:7c88a4a7926ee5145ad2dfa06f454c67` (Unity Engine)

**No internal TUA dependencies**

---

#### 2. **TUA_I18n** (`Assets/Scripts/I18n/`)
**References:**
- `GUID:7c88a4a7926ee5145ad2dfa06f454c67` (Unity Engine)

**No internal TUA dependencies**

---

#### 3. **TUA_Core** (`Assets/Scripts/Core/`)
**References:**
- `GUID:2e99c95bbb9ddd5439acfb79b643b82e` (FishNet.Runtime)
- `GUID:7c88a4a7926ee5145ad2dfa06f454c67` (Unity Engine)
- `GUID:90b214ac35bdc5d4cafb8e07a2dbfac5` (TUA_Misc)

**Internal Dependencies:**
- TUA_Misc (for NetBehaviour, SingletonNetBehaviour, Uuid, WeaponState, Registry, RegistrableScriptableObject)

---

#### 4. **TUA_Items** (`Assets/Scripts/Items/`)
**References:**
- `GUID:7c88a4a7926ee5145ad2dfa06f454c67` (Unity Engine)
- `GUID:4c78612b603e2c4478edfb1893d7a138` (TUA_Core)

**Internal Dependencies:**
- TUA_Core (for Item, ItemStack, Registry, RegistrableScriptableObject, Interfaces)

---

#### 5. **TUA_Settings** (`Assets/Scripts/Settings/`)
**References:**
- `GUID:a70a4983dc551454382207c5fe1a4649` (Unity UI Toolkit)

**No internal TUA dependencies**

---

#### 6. **TUA_Entities** (`Assets/Scripts/Entities/`)
**References:**
- `GUID:2e99c95bbb9ddd5439acfb79b643b82e` (FishNet.Runtime)
- `GUID:7c88a4a7926ee5145ad2dfa06f454c67` (Unity Engine)
- `GUID:4c78612b603e2c4478edfb1893d7a138` (TUA_Core)
- `GUID:073a12aeff2cd3044a6830e60d91af04` (TUA_Items)
- `GUID:90b214ac35bdc5d4cafb8e07a2dbfac5` (TUA_Misc)
- `GUID:7f7d1af65c2641843945d409d28f2e20` (TUA_Settings)
- Unity Animation Rigging GUID (check Unity package manager)

**Internal Dependencies:**
- TUA_Core (Entity, GameWorld, Interfaces, Inventory)
- TUA_Items (for item types)
- TUA_Misc (NetBehaviour, SingletonNetBehaviour, Uuid, WeaponState)
- TUA_Settings (for PlayerEntity settings)

---

#### 7. **TUA_Systems** (`Assets/Scripts/Systems/`)
**References:**
- `GUID:2e99c95bbb9ddd5439acfb79b643b82e` (FishNet.Runtime)
- `GUID:7c88a4a7926ee5145ad2dfa06f454c67` (Unity Engine)
- `GUID:4c78612b603e2c4478edfb1893d7a138` (TUA_Core)
- `GUID:039228a55c00e6c40a1b8efe4d10d3a7` (TUA_Entities - check actual GUID)
- `GUID:073a12aeff2cd3044a6830e60d91af04` (TUA_Items)
- `GUID:90b214ac35bdc5d4cafb8e07a2dbfac5` (TUA_Misc)

**Internal Dependencies:**
- TUA_Core (GameWorld, Entity, Inventory, Interfaces)
- TUA_Entities (PlayerEntity, HackingTarget)
- TUA_Items (WeaponItemStack, DataDriveItemStack, HackerToolItem)
- TUA_Misc (SingletonNetBehaviour, NetBehaviour, Uuid, WeaponState)

---

#### 8. **TUA_UI** (`Assets/Scripts/UI/`)
**References:**
- `GUID:2e99c95bbb9ddd5439acfb79b643b82e` (FishNet.Runtime)
- `GUID:7c88a4a7926ee5145ad2dfa06f454c67` (Unity Engine)
- `GUID:4c78612b603e2c4478edfb1893d7a138` (TUA_Core)
- `GUID:039228a55c00e6c40a1b8efe4d10d3a7` (TUA_Entities - check actual GUID)
- `GUID:073a12aeff2cd3044a6830e60d91af04` (TUA_Items)
- `GUID:90b214ac35bdc5d4cafb8e07a2dbfac5` (TUA_Misc)
- `GUID:039228a55c00e6c40a1b8efe4d10d3a7` (TUA_Systems - check actual GUID)
- `GUID:a70a4983dc551454382207c5fe1a4649` (Unity UI Toolkit)
- `GUID:660479ac9f1237849ad10a69059a6d40` (Unity UI - legacy)
- TUA_I18n GUID (check actual GUID)
- `GUID:7f7d1af65c2641843945d409d28f2e20` (TUA_Settings)

**Internal Dependencies:**
- TUA_Core (GameWorld, Entity, Inventory, Interfaces)
- TUA_Entities (PlayerEntity)
- TUA_Items (WeaponItemStack, HackerToolItem)
- TUA_Misc (SingletonBehaviour, Uuid)
- TUA_Systems (WeaponSystem, FeedSystem, HackingSystem)
- TUA_I18n (LocalizationManager)
- TUA_Settings (for SettingsMenuController)

---

#### 9. **TUA_GameModes** (`Assets/Scripts/GameModes/`) - *Not yet created*
**References:**
- `GUID:7c88a4a7926ee5145ad2dfa06f454c67` (Unity Engine)
- `GUID:4c78612b603e2c4478edfb1893d7a138` (TUA_Core)
- `GUID:039228a55c00e6c40a1b8efe4d10d3a7` (TUA_Entities - check actual GUID)
- `GUID:073a12aeff2cd3044a6830e60d91af04` (TUA_Items)

**Internal Dependencies:**
- TUA_Core (GameMode, GameWorld, Inventory)
- TUA_Entities (PlayerEntity)
- TUA_Items (WeaponItemStack, ItemStack)

---

#### 10. **TUA_Serializers** (Root: `Assets/Scripts/Serializers_ImplNet.cs`) - *Not yet created*
**References:**
- `GUID:2e99c95bbb9ddd5439acfb79b643b82e` (FishNet.Runtime)
- `GUID:7c88a4a7926ee5145ad2dfa06f454c67` (Unity Engine)
- `GUID:4c78612b603e2c4478edfb1893d7a138` (TUA_Core)
- `GUID:073a12aeff2cd3044a6830e60d91af04` (TUA_Items)
- `GUID:90b214ac35bdc5d4cafb8e07a2dbfac5` (TUA_Misc)

**Internal Dependencies:**
- TUA_Core (ItemStack, Inventory, GamePlayer)
- TUA_Items (WeaponItemStack, DataDriveItemStack)
- TUA_Misc (Uuid)

**Note:** Currently a single file. Could be moved to `Core/` or a new `Serializers/` folder.

### Option 2: With GameModes and Serializers (10 asmdefs)
Add:
9. **TUA_GameModes** - References: TUA_Core, TUA_Entities, TUA_Items, FishNet
10. **TUA_Serializers** - References: TUA_Core, TUA_Items, TUA_Misc, FishNet

---

## Dependency Hierarchy (Bottom to Top)

```
Level 0 (No TUA dependencies):
├── TUA_Misc (only FishNet)
└── TUA_I18n (only Unity)

Level 1 (Depends on Level 0):
├── TUA_Core (depends on TUA_Misc)
├── TUA_Items (depends on TUA_Core)
└── TUA_Settings (no TUA deps)

Level 2 (Depends on Level 1):
├── TUA_Entities (depends on TUA_Core, TUA_Items, TUA_Misc, TUA_Settings)
└── TUA_Serializers (depends on TUA_Core, TUA_Items, TUA_Misc)

Level 3 (Depends on Level 2):
├── TUA_Systems (depends on TUA_Core, TUA_Entities, TUA_Items, TUA_Misc)
└── TUA_GameModes (depends on TUA_Core, TUA_Entities, TUA_Items)

Level 4 (Top level):
└── TUA_UI (depends on almost everything)
```

---

## Potential Circular Dependencies

**None detected!** The dependency graph is acyclic:
- Core.Interfaces is part of Core (no separate asmdef)
- All dependencies flow downward in the hierarchy
- No folder depends on a folder that depends back on it

---

## External Dependencies (GUIDs)

Common external assembly GUIDs (from existing asmdefs):
- `2e99c95bbb9ddd5439acfb79b643b82e` - FishNet.Runtime
- `7c88a4a7926ee5145ad2dfa06f454c67` - Unity Engine
- `4c78612b603e2c4478edfb1893d7a138` - TUA_Core (internal)
- `073a12aeff2cd3044a6830e60d91af04` - TUA_Items (internal)
- `90b214ac35bdc5d4cafb8e07a2dbfac5` - TUA_Misc (internal)
- `7f7d1af65c2641843945d409d28f2e20` - TUA_Settings (internal)
- `039228a55c00e6c40a1b8efe4d10d3a7` - TUA_Systems (internal)
- `a70a4983dc551454382207c5fe1a4649` - Unity UI Toolkit
- `660479ac9f1237849ad10a69059a6d40` - Unity UI (legacy)

---

## Recommendations

1. **Keep current structure** - It's well-organized and avoids circular dependencies
2. **Create TUA_GameModes** - If the GameModes folder grows beyond DefaultGameMode
3. **Consider moving Serializers_ImplNet.cs** - Either into Core folder or create a Serializers folder
4. **Verify GUIDs** - When creating/updating asmdefs, ensure GUIDs match actual assembly names
