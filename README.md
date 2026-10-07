# Robo Lạc Lối

A small spherical robot rolls through a low-poly post-apocalyptic world that nature is taking back,
collecting energy cores to reach its home base. Made with Unity **2020.3.19f1**.

Zones: Bãi phế liệu (Level1), Khu công nghiệp bỏ hoang (Level2), Vùng hoang hóa (Level3), Trạm căn cứ (Level4).

## Setup

1. Install [Git LFS](https://git-lfs.com) before cloning — models, textures, audio and fonts are stored with LFS:
   ```bash
   git lfs install
   git clone https://github.com/hongAn-dev/game3DD.git
   ```
2. Open the project with Unity 2020.3.19f1 (Unity Hub > Add > select the project folder).
3. Open `Assets/Scenes/MainMenu.unity` and press Play.

Linux build (do not pass `-nographics`, shaders would not be compiled and the game renders magenta):

```bash
unity -batchmode -quit -projectPath . -executeMethod BuildScript.BuildLinux
```

Output: `Builds/Linux/RoboLacLoi.x86_64`.

## Assets

| Pack | Author | License | Used for |
|------|--------|---------|----------|
| [Nature Kit](https://kenney.nl/assets/nature-kit) | Kenney | CC0 | Trees, bushes, grass, plants, logs, rocks (`Assets/ThirdParty/KenneyNatureKit`) |
| [Robot Enemy](https://poly.pizza/m/1gNo5ezvmr) | Quaternius | CC0 | Patrol robot enemy (`Assets/ThirdParty/QuaterniusRobotEnemy`) |
| [City Kit (Industrial)](https://kenney.nl/assets/city-kit-industrial) | Kenney | CC0 | Scrapyard / industrial / base props (`Assets/ThirdParty/KenneyCityKitIndustrial`) |
| [Survival Kit](https://kenney.nl/assets/survival-kit) | Kenney | CC0 | Barrels, boxes, metal panels (`Assets/ThirdParty/KenneySurvivalKit`) |
| [Space Station Kit](https://kenney.nl/assets/space-station-kit) | Kenney | CC0 | Containers, pipes, base structures (`Assets/ThirdParty/KenneySpaceStationKit`) |
| [Sci-fi](https://kenney.nl/assets/sci-fi-sounds) / [Interface](https://kenney.nl/assets/interface-sounds) Sounds | Kenney | CC0 | Pickup, explosion, win/lose, click, ambience (`Assets/ThirdParty/KenneyAudio`) |
| Robot ball, energy core | this project (`Tools/blender/*.py`) | MIT | Player and pickups (`Assets/ThirdParty/RoboLacLoi`) |
| [Chakra Petch](https://fonts.google.com/specimen/Chakra+Petch) | Cadson Demak | OFL 1.1 | Sci-fi UI font with Vietnamese glyphs (`Assets/Fonts/ChakraPetch`) |

The look is applied by editor scripts, all runnable from the `Tools` menu or in batch mode with `-executeMethod`:

- `AssetReplacer.ReplaceAll` — player, energy cores, patrol robot, scrap, nature props
- `ZoneDresser.DressAll` — per-level terrain colors, sky, fog, lights, decorations, ambience
- `EffectsTheme.Apply` — explosion colors and sounds
- `UiTheme.Apply` — fonts, Vietnamese text, HUD

## License

MIT, see [LICENSE](LICENSE). Based on LiBot Adventure by Vin Busquet (MIT).
