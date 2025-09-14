# Better Grenade Handling v0.1.0

RimWorld mod that enhances the vanilla AI behavior in combat   

🚧 **This is WIP.** I just started learning C# and RimWorld API 2 weeks ago as of writing this README, so if you have a better approach - pull requests and suggestions are welcome.
If you encounter any issues, bugs or performance drops - let me know.
## Features

- Pawns will avoid shooting or throwing an explosive if there is at least 1 ally in blast radius. Including friendly factions/caravans too.
- Pawns will, however, launch incendiary weapons, such as molotov, if overall heat armor is >90%
- Pawns will now launch EMP weapons at enemies wearing shield-belts. A capability the vanilla game clearly lacks.
- All of the above applies to enemy raiders too

## 📥 Installation

You can download it on Steam via *insert steam link here*

For non-steam users:
1. Download the latest release
2. Unpack into RimWorld/Mods folder
3. This mod also requires Harmony library, please install https://github.com/pardeike/HarmonyRimWorld
