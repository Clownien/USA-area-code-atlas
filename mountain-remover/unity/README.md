# Mountain Remover (Unity)

A first-person mining game. Dig a mountain block by block, sell what you mine, buy drill rigs and
machinery, and work your way up to removing Mount Everest.

The whole game is built from code when you press Play, so there are no scenes, prefabs or art
files to set up.

## Setup (about 2 minutes)

1. Open **Unity Hub** and click **New project**.
2. Pick the **3D (Built-In Render Pipeline)** template. **Universal 3D (URP)** also works.
3. Name it `MountainRemover` and click **Create project**. Wait for the editor to open.
4. In the editor's **Project** window, right-click **Assets** and choose
   **Show in Explorer** (Windows) or **Reveal in Finder** (Mac).
5. Copy the `MountainRemover` folder from this download's `Assets` folder into that `Assets` folder.
   You should end up with `Assets/MountainRemover/Scripts/...`.
6. Switch back to Unity and wait for it to finish compiling (the spinner at the bottom right).
7. Press **Play** (the ▶ button at the top). Click **Start mining** in the Game view.

Tip: to play at full size, click the **Game** tab's dropdown and turn on **Play Maximized**,
or press **Shift + Space** with the mouse over the Game view.

## Controls

| Key | Action |
| --- | --- |
| W A S D | Move |
| Mouse | Look |
| Left click (hold) | Mine with the pickaxe, or throw dynamite |
| Space | Jump |
| Shift | Sprint |
| E | Sell your backpack at the trading post, or ship the silo |
| 1 / 2 (or Q, or scroll) | Switch between pickaxe and dynamite |
| B | Open or close the shop |
| Esc | Pause (frees the mouse) |
| M | Mute |

## How to play

- Mine blocks with the pickaxe. They go into your backpack.
- Walk to the **trading post** (the wooden hut behind where you start) and press **E** to sell.
- Press **B** to open the shop. Buy a better pickaxe and more carrying capacity first.
- **Drill rigs** drive up to the mountain on their own and mine from the top down into the
  **silo**. Ship the silo by hand with **E**, or build the **conveyor** to sell it automatically.
- The **crusher** and **smelter** raise how much everything sells for.
- **Dynamite** blasts a sphere out of the mountain and sends the debris to the silo.
- When 98% of a mountain is gone, your crew clears the rest and you travel to the next one.
  Your money and machines come with you.

The five mountains: Small Hill, Rocky Mountain, Large Mountain, the Matterhorn and Mount Everest.

The game saves automatically every 15 seconds, when you sell, and when you stop playing. The save
file is `mountain-remover-save.json` in Unity's persistent data folder. Use **New game** on the
pause menu to start over.

## Where things are in the code

| File | What it does |
| --- | --- |
| `Data.cs` | All tuning: materials, mountains, upgrade prices and rates. Change numbers here to rebalance. |
| `VoxelWorld.cs` | Mountain generation, chunk meshing, block removal, ray casting. |
| `Player.cs` | Walking, jumping, block collision, camera and the held pickaxe. |
| `GameManager.cs` | Main loop, mining, dynamite, drill rigs, selling, progression, saving. |
| `Scenery.cs` | Trading post, silo, conveyor, crusher, smelter, rigs, trees, distant peaks. |
| `GameUI.cs` | HUD, menus and shop (Unity's immediate-mode GUI). |
| `Fx.cs` | Rock chips, explosions, and sound effects generated in code. |
| `InputShim.cs` | Works with both the new Input System and the old Input Manager. |

## Troubleshooting

- **Everything is pink:** the materials couldn't find a shader. This happens if the project uses
  HDRP. Create the project with the Built-In or Universal 3D template instead.
- **Errors mentioning `Input`:** go to **Edit → Project Settings → Player → Other Settings →
  Active Input Handling** and set it to **Both**, then let Unity restart.
- **The mouse doesn't look around:** click inside the Game view once so it captures the mouse.
- **Making a standalone build** (File → Build): add `Standard` (Built-In) or
  `Universal Render Pipeline/Lit` and `Unlit` (URP) to **Project Settings → Graphics → Always
  Included Shaders**, because the game creates its materials from code. Playing in the editor
  doesn't need this.
