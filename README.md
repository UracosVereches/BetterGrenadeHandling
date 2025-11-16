# Better Grenade Handling

RimWorld mod that enhances the vanilla AI behavior in combat

## Features

- Pawns will avoid launching an explosive if there is at least 1 ally in blast radius + forced miss radius, including friendly factions/caravans too.
- Pawns will, however, launch incendiary projectiles, such as molotov, if overall heat armor of an ally is >90% or flammability is less than 10% (impids, phoenix armor)
- Pawns will launch toxic projectiles if allies in affected area have sufficient protection, like gasmask.
Toxic raiders launch toxic projectiles no matter what. 
- Pawns avoid traversing over area where explosive is about to impact or is already present. Only works for friendly factions. **Mortars are supported**
- If pawn is forced to launch an explosive(bypasses friendly fire checks) then any ally that happens to be in the blast area will flee out of it. **Mortars are supported**
- Pawns equpped with EMP won't target already EMP-stunned mechs - eliminates the dumb behavior when they target the same mech over and over again. Also they automatically target enemies wearing shield-belts - a capability the vanilla game clearly lacks.
- Pawns try to cause as much damage with explosives as possible by choosing much more tightly packed groups of enemies through the in-game target score system. Increases the DPS.
- All of the above applies to enemy raiders too. Now they will value their own lives more and won't shoot rockets at you if it involves any friendly fire.
- You can customize some parts of the mod to your liking. Supported languages: English and Russian.
- Meticulously overengineered

## 📥 Installation

You can download it on Steam via https://steamcommunity.com/sharedfiles/filedetails/?id=3592500869

For non-steam users:
1. Download the latest release
2. Unpack into RimWorld/Mods folder
3. This mod also requires Harmony library, please install https://github.com/pardeike/HarmonyRimWorld

## For modders
Building in Debug mode enables helpful on-screen UI that visualizes code in real time

If you have a better approach - pull requests and suggestions are highly welcome.

If you encounter any issues, bugs or performance drops - let me know.
