# Shadow Drift for CarX

A standalone movement tool for **CarX Drift Racing Online**, created by **rez**.

Shadow Drift follows another player’s recorded driving line with an adjustable delay. It includes adaptive spacing, wheel animation and drivetrain matching, plus an option to return driving control at the leader’s speed.

## Features

- Follow a selected player’s recorded position and rotation.
- Adjust the base follow delay from **0.01 to 0.30 seconds**.
- Enable adaptive spacing to give the cars more room during transitions.
- Adjust extra delay and body clearance.
- Match delayed front-wheel steering, wheel spin and drivetrain state.
- Join the leader’s line, then take over using your driving controls.
- Stop and return to your starting position with **F9**.
- Change the menu key from the launcher or in-game menu.
- Save the menu key between sessions.
- Use without installing RCLI or Kino.

## Requirements

- Windows x64.
- CarX Drift Racing Online installed through Steam.
- An active multiplayer lobby.
- Your car and the selected player’s car fully loaded.

This release targets the game assemblies inspected during development. Game updates may require a matching tool update.

## Download and installation

1. Open this repository’s **Releases** page.
2. Download the attached release archive.
3. Extract the archive into its own folder.
4. Keep these files together:

   ```text
   Shadow Drift/
   ├── Shadow Drift.exe
   ├── Shadow-Drift.Mod.dll
   ├── README.md
   └── Licenses/
   ```

5. Run **Shadow Drift.exe**.
6. Select **Launch CarX & load tool**.

The launcher can start CarX through Steam or connect to an already running game. The tool does not replace game files.

## Quick start

1. Join a multiplayer lobby.
2. Wait for your car and the other players’ cars to finish loading.
3. Open the menu with **F8**, unless you changed the menu key.
4. Search for and select a leader.
5. Choose your follow delay.
6. Select **Play delayed shadow**.
7. Hide the menu to view the movement clearly.

Press **F9** to stop and return to the position where following started.

## Follow settings

### Delay

Controls how far behind the leader’s recorded movement your car follows.

- A smaller delay places your car closer to the leader’s current position.
- A larger delay follows an earlier point along the recorded route.
- Base delay ranges from **0.01 to 0.30 seconds**.
- Use **Apply delay / restart** to restart following with the selected delay.

A very small delay does not guarantee a clear position between cars. Adaptive spacing may increase the actual delay.

### Adaptive spacing

Adjusts the follow timing to allow additional room during drift transitions.

Available controls include:

- **Extra delay:** additional separation during transitions or manual back-off.
- **Body margin:** extra clearance around the estimated car footprints.
- **Right Shift:** hold to request additional back-off.

The menu displays the current timing status. Adaptive spacing uses recorded movement and estimated car dimensions; it cannot guarantee that cars will never overlap.

### Wheel and drivetrain options

These options can be enabled separately:

| Option | Purpose |
| --- | --- |
| Animate wheel spin | Replays wheel rotation from the delayed movement |
| Match delayed front-wheel steering | Matches the leader’s recorded steering |
| Synchronize shadow drivetrain | Matches supported drivetrain values during following |

Availability depends on the car and the game information accessible to the tool. Check the menu for any unavailable-feature messages.

## Join leader / drive away

Use **Join leader / drive away** to enter the leader’s recorded line and then resume driving.

1. Select a leader.
2. Click **Join leader / drive away**.
3. Release your driving controls while the handoff arms.
4. Once the menu indicates that takeover is armed, steer, accelerate or brake to regain control.

The tool attempts to return control while preserving the current movement speed.

**F9** provides a separate stop action that returns you to your starting position.

## Controls

| Control | Action |
| --- | --- |
| F8, by default | Open or close the menu |
| F9 | Stop following and return to the starting position |
| Right Shift | Request back-off while adaptive spacing is active |
| Hide menu | Close the panel while keeping the tool loaded |
| Unload tool | Stop and unload the tool |

The menu pauses normal game input while it is open.

## Changing the menu key

Change the key in either place:

- **Desktop launcher → Menu key**
- **In-game menu → Menu key**

The preference is saved separately from Rocket League Movement.

Changes made in the launcher are checked by the loaded tool approximately once per second. If the other standalone movement tool already uses the selected key, Shadow Drift keeps its current key. Choose another key.

**F9 and F10 are reserved for movement stop actions** and are not offered as menu keys.

## Using alongside other mods

Shadow Drift and the standalone Rocket League Movement tool can both be loaded, but only one of their movement modes can control the car at a time.

Before starting Shadow Drift:

- Stop active movement modes in RCLI.
- Disable movement from the original RL mod or other movement tools.
- Avoid switching cars or changing sessions while following.

The tool stops following when it detects that the selected player left, a relevant car changed or the session changed.

## Multiplayer behavior

Shadow Drift controls your local car using another player’s received movement data. It does not control the leader’s car.

The leader’s network updates, loading state and connection quality affect the recorded route. Other players’ view of your movement depends on the game’s normal replication.

Remote appearance, controller behavior and live multiplayer operation still require in-game testing.

## Updating

1. Stop following.
2. Close CarX and the Shadow Drift launcher.
3. Extract the new release.
4. Replace the launcher and mod DLL together.
5. Start the updated launcher.

Do not replace only one binary or mix files from different releases.

## Troubleshooting

### The launcher reports a missing DLL

Keep `Shadow-Drift.Mod.dll` beside `Shadow Drift.exe`. Extract the archive before running the launcher.

### The menu does not open

- Return to CarX and allow it to finish loading.
- Check the menu key shown in the launcher.
- Choose another key if it conflicts with another mod.
- Check whether the launcher reported a loading error.

### No players appear

Join a multiplayer lobby and wait for the player roster and cars to load. Shadow Drift requires another player as its leader.

### Following does not start

- Make sure both cars are fully loaded.
- Leave the garage and spawn into the driving session.
- Stop other active movement modes.
- Read the status message in the menu.

### The cars overlap or following looks unstable

- Increase the base delay.
- Enable adaptive spacing.
- Increase the extra delay or body margin.
- Allow more time for the leader’s movement to load.

Network instability and unusual vehicle dimensions can affect the result.

### The leader changes car or leaves

Following should stop automatically. Wait for the new car to load, then select a leader again.

### Car control does not return correctly

Press **F9**. If the menu reports a restoration error, respawn your car. Restart CarX if the problem continues.

## Reporting an issue

Include:

- Shadow Drift release version.
- CarX version.
- Track and lobby details.
- Other movement mods loaded.
- Selected delay and adaptive spacing settings.
- The exact menu or launcher error.
- Steps to reproduce the problem.
- A short video, if possible.

## Development status

This is a **beta release**.

Builds, launcher connection self-tests and automated movement logic checks passed. These checks do not replace live testing in CarX.

## Credits

Created by **rez**.

Unofficial tool for CarX Drift Racing Online. Not affiliated with or endorsed by the game’s developers.

Includes HarmonyX, Mono.Cecil and MonoMod components. Third-party license notices are included in the `Licenses` folder.
