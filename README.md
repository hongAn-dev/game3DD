# game3DD

3D ball adventure game made with Unity **2020.3.19f1**.

## Setup

1. Install [Git LFS](https://git-lfs.com) before cloning — models, textures, audio and fonts are stored with LFS:
   ```bash
   git lfs install
   git clone https://github.com/hongAn-dev/game3DD.git
   ```
2. Open the project with Unity 2020.3.19f1 (Unity Hub > Add > select the project folder).
3. Open `Assets/Scenes/MainMenu.unity` and press Play.

## Assets

| Pack | Author | License | Used for |
|------|--------|---------|----------|
| [Nature Kit](https://kenney.nl/assets/nature-kit) | Kenney | CC0 | Trees, bushes, grass, plants, logs, rocks (`Assets/ThirdParty/KenneyNatureKit`) |
| [Ultimate Monsters](https://quaternius.com/packs/ultimatemonsters.html) | Quaternius | CC0 | Chasing monster enemy (`Assets/ThirdParty/QuaterniusUltimateMonsters`) |

The models were swapped in with `Tools > Replace Assets (Kenney + Quaternius)` (`Assets/Editor/AssetReplacer.cs`).

## License

MIT, see [LICENSE](LICENSE). Based on LiBot Adventure by Vin Busquet (MIT).
