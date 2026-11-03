# Thereasm

<p align="center">
  <img src="Docs/Media/banner.webp" alt="Thereasm Banner" width="100%">
</p>

A 2-player strategic deduction card game made with Unity. Play locally against AI bots or challenge a friend online via Photon Fusion.

---

## Gameplay

![Thereasm Gameplay](Docs/Media/gameplay.webp)

- **Energy Management:** Spend hearts wisely across playing cards, repositioning units, and triggering skills.
- **Neutral Cards:** Utilize neutral cards on the board to swing votes, gather info, or set up tactical plays.
- **Asymmetric Roles:** 
  - The **Cursermancer** works in the dark to spread curses and mislead the opponent.
  - The **Village** tracks movements, analyzes votes, and unmasks the hidden curse before running out of time.

<br>

<table align="center">
  <tr>
    <td width="50%" align="center">
      <img src="Docs/Media/faction-village.webp" alt="Village" width="100"><br>
      <h4>Village</h4>
      Track board movements, analyze vote patterns, and identify the cursed cards.
    </td>
    <td width="50%" align="center">
      <img src="Docs/Media/faction-cur.webp" alt="Cursermancer" width="100"><br>
      <h4>Cursermancer</h4>
      Spread hidden curses quietly, bluff, and trick the village into sacrificing the wrong target.
    </td>
  </tr>
</table>

---

## Screenshots & Tutorial

The game features an interactive tutorial academy and an in-game investigation notebook to help you track suspects during matches.

<p align="center">
  <img src="Docs/Media/academy.webp" alt="Tutorial Academy" width="49%">
  <img src="Docs/Media/notebook.webp" alt="Investigation Notebook" width="49%">
</p>

<details>
<summary><strong>View Main Menu</strong></summary>

<p align="center">
  <img src="Docs/Media/menu.webp" alt="Main Menu" width="80%">
</p>

</details>

---

## Getting Started

### Prerequisites
- **Unity 6** (Recommended version: `6000.0.29f1` or later) with Windows Build Support.
- **Photon Fusion 2.0.13** (SDK package downloaded from Photon Engine).

### Setup & Run
1. Clone the repository:
   ```bash
   git clone https://github.com/evilogre1112/Thereasm.git
   ```
2. Open the project folder in **Unity Hub** and allow Unity to import the packages and assets.
3. Import Photon Fusion:
   - In Unity, go to **Assets → Import Package → Custom Package** and choose the `Fusion 2.0.13` unitypackage.
   - *(Note: Keep the project's existing `PhotonAppSettings.asset` and `NetworkProjectConfig.fusion` when importing).*
4. Open the main menu scene at `Assets/Scenes/MenuScenes.unity` and press **Play**.

### Online Multiplayer Setup
To play with a friend over the internet:
- Open `Assets/Networking/PhotonOnlineConfig.asset` and input your Photon Fusion App ID.
- Both players must use the same build version and region. One creates a room code and the other joins via the code.

---

## Controls

| Action | Control |
|---|---|
| Select / Move / Place / Vote | **Left Click** / Drag |
| Inspect card / Aim skill | **Right Click** / Right Drag |
| Zoom in / out | **Mouse Wheel** |
| Pan board | **Middle Mouse Drag** or drag empty space |
| Reset board view | **Home** |
| Pause / Game Rules | **Esc** |
