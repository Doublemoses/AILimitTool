# AI LImit Speedrun Practice Tool
A small tool designed to help speedrunners and challenge runners practice AI Limit. Supports hotkeys that are only active while the AI Limit window is on top. Hotkeys are currently hardcoded, but will be editable in the next release. Hotkeys are written next to the command, as well as right square bracket (]) being used to cycle through tabs.

## Features
### Main Tab
**Immortal** (Hotkey: P) - Makes the player unkillable

**Lock Sync** (Hotkey: O) - Makes the player's sync level unable to change from its current value

**Lock Target HP** (Hotkey: I) - Locks the HP of the currently targetted enemy to a % value defined by the player

**Infinite Healing Dew** - Removes the charge limit on healing dew

**Movement Speed** (Hotkey: L) - Increases the player's movement speed to one defined by the player

**Free mainhand weapon upgraades** - Removes the upgrade cost, but currently.only for the weapon held in the main hand. Can take a slight bit of time to take effect again after performing an upgrade.

**Target Info Bos** (Hotkey: Ctrl + B) - Creates a small always-on-top window that shows the data from the current target, including HP, sync, tenacity, status buildup. Other checkboxes set optional data such as resists and super armour.

**Boss Respawn** - Can respawn most bosses. Aether, the boss rush, and optional side bosses are not supported yet.

### Stats Tab

**Set Player Stats** - Set the five main player stats, and level. By default, the correct player level will be calculated based on stats entered.

**Set Crystals** - Set crystals to the desired amount. The number will not visually change on screen until the UI is refreshed.

**Add Crystals** (Hotkey: K) - Adds a number of crystals, determined by the player (default: 10 000).

### Teleport Tab

**Quicksave Teleport Location** (Hotkey: Shift + M) - Saves the current player coordinates. This only works for the current map. To teleport to another map, use Set Broken Branch Destination.

**Quickload Teleport Location** (Hotkey: M) - Teleports the player to the saved location. **Note:** If the player is moving, the hotkey should be held down until the teleport happens, which may take a few seconds.

**Save Positions** - Positions can be saved into a database for use later on. Will only show teleport points for the current level.

**Set Broken Branch Destination** - This sets the location you will spawn at if you die or use the broken branch item. Use this to teleport between levels.

### States Tab

This tab is used to track game and monster states, which controls the game logic.

Game states have an ID and a value. Some things they cover include item pickups, where elevators are, and if doors have been opened.

Monster states have and ID, the levelID they are associated with, and a value. One thing they cover is if bosses have been killed.
