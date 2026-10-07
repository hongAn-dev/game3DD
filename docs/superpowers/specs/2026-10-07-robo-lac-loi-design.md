# Robo Lạc Lối — thiết kế triển khai

Ngày: 2026-10-07 · Nguồn ý tưởng: `mota.md` (định hướng mỹ thuật "Robo lạc lối")

## 1. Mục tiêu

Biến game3DD (game bóng lăn dựa trên LiBot Adventure, Unity 2020.3) thành **"Robo Lạc Lối"**:
một robot cầu nhỏ lăn qua thế giới hậu tận thế low-poly đang được cây cỏ phủ lại, thu thập lõi năng lượng
để về tới trạm căn cứ.

- **Mục đích:** đồ án nhóm, dùng để demo.
- **Thiết bị demo:** PC (bàn phím + chuột). Không build Android trong phạm vi này.
- **Phạm vi:** đủ 4 khu vực, hiệu ứng, âm thanh, UI bảng điều khiển. Không giới hạn thời gian.
- **Thành công khi:** cả 4 màn chơi được từ menu tới màn cuối; robot, lõi năng lượng và nguy hiểm nhìn rõ
  dưới camera gameplay; hình ảnh, âm thanh và chữ thống nhất một chủ đề; bản build PC chạy được.

## 2. Ràng buộc

- Unity **2020.3.19f1**, Built-in Render Pipeline, Input Manager cũ (CrossPlatformInput).
- Asset chỉ dùng giấy phép **CC0** (Kenney, Quaternius); font dùng OpenSans có sẵn trong repo (Apache 2.0). Ghi nguồn trong `README.md`
  và `License.txt` của từng thư mục `Assets/ThirdParty/*`.
- **Giữ nguyên luật chơi**: điểm cần để qua màn theo độ khó, chết khi rơi nước/vực, checkpoint, sinh địch
  ngẫu nhiên. Không thêm cơ chế tương tác mới; cổng, trạm sạc chỉ để trang trí (trừ hiệu ứng sáng khi thắng).
- **Giữ nguyên địa hình và đường đi** của 4 màn để không phải cân bằng lại độ khó.
- Script lõi giữ nguyên interface: `GameManager`, `Health`, `Damage`, `SpawnGameObjects`, `SetupLevel`,
  `BallUserControl`, `MonsterChaser`, `Treasure`.

## 3. Thiết kế

### 3.1 Cách thay asset

Thay ở **cấp prefab**, vì các scene đều tham chiếu prefab; đổi một prefab là đổi ở cả 4 màn.
Dùng lại `Assets/Editor/AssetReplacer.cs` (đã có hàm `ReplaceVisual` giữ tỉ lệ và collider).

| Prefab | Thay bằng |
|---|---|
| `Player` | Robot cầu tự dựng (Blender), bán kính 0.5 khớp `SphereCollider`; trail màu cyan |
| `Coin`, `Coin Bouncy`, `Sil Coins`, `Vin Coins` | Lõi năng lượng tự dựng (Blender), to khoảng 1.3 lần xu cũ |
| `Enemy - Monster` | Robot tuần tra Quaternius có clip chạy/tấn công, mắt đỏ |
| `Enemy - Crater` | Phế liệu rơi: thùng/khối kim loại Kenney |

**Robot người chơi:** vỏ cầu bạc xám chia 6 mảng, dải đèn cyan quanh xích đạo, một mắt đèn vàng ấm,
vài vết gỉ nâu. Mắt gắn trên thân và xoay theo khi lăn (chấp nhận, theo gợi ý trong `mota.md`).

**Lõi năng lượng:** tinh thể cyan có emission trong khung kim loại 4 thanh; xoay bằng script `Rotate` sẵn có.
Màu và hình khối phải dễ nhìn ngay cả khi không có bloom.

### 3.2 Bốn khu vực

| Màn | Khu | Tông màu | Asset trang trí chính | Điểm nhấn |
|---|---|---|---|---|
| Level1 | Bãi phế liệu | vàng bụi, nâu gỉ | Kenney Survival Kit, City Kit Industrial: thùng hàng, ống, bánh răng | — |
| Level2 | Khu công nghiệp bỏ hoang | xám lạnh, đỏ gỉ | City Kit Industrial: bồn chứa, hàng rào, nhà xưởng | — |
| Level3 | Vùng hoang hóa | xanh rêu, bê tông | Nature Kit + mảnh bê tông vỡ | — |
| Level4 | Trạm căn cứ | xám xanh, đèn tín hiệu | Space Station Kit / Modular Space Kit: ăng-ten, máy phát, đèn | trạm sạc sáng lên khi thắng |

Mỗi scene:

1. Đổi material/màu địa hình.
2. Đổi skybox (procedural), fog và ánh sáng.
3. Thay đồ trang trí sẵn có (instance của `Tree_*`, `Rock_*`, `Stone_1`, `Bush_*`, `Plant_*`, `Log_*`…)
   bằng đồ của khu, **giữ đúng vị trí, hướng và diện tích chiếm đất**. Các vị trí này do người thiết kế màn gốc
   đặt, đã nằm ngoài đường đi, nên không phải tìm chỗ mới. Mỗi khu có bảng "prefab cũ → model mới" riêng.
   Level3 (Vùng hoang hóa) giữ phần lớn cây cỏ, chỉ thay đá nhỏ thành mảnh bê tông/phế liệu.

Việc thay làm bằng Editor script với bảng ánh xạ trong code, để tái lập và chỉnh được.
Mỗi màn hiển thị tên khu trên HUD và thẻ giới thiệu.

### 3.3 Hiệu ứng

Sửa 3 particle prefab sẵn có:

| Prefab | Hiệu ứng mới |
|---|---|
| `ExplodeCoin Particle` | lóe cyan |
| `ExplodeEnemy Particle` | khói xám + tia lửa cam |
| `ExplodePlayer Particle` | tia lửa đỏ |

Robot người chơi:

- Đèn cyan sáng dần theo tỉ lệ năng lượng đã thu.
- Chớp đỏ khi trúng đòn.

Script mới: `RobotLights`, đọc `GameManager.gm.score` và chỉ tiêu điểm của màn.

### 3.4 Âm thanh

- **Hiệu ứng:** Kenney Sci-fi Sounds, Impact Sounds, Interface Sounds. Gồm: nhặt năng lượng, va chạm, địch nổ,
  thắng (đủ năng lượng), thua (mất kết nối), bấm nút.
- **Nhạc nền:** giữ các bản nhạc riêng từng màn có sẵn (`Assets/Audio/Level1..4.ogg`, gán qua
  `GameManager.backgroundMusic`). Thêm tiếng nền môi trường (gió, máy chạy xa) bằng âm Kenney phát lặp nhỏ.

### 3.5 UI

- Tên game: **"Robo Lạc Lối"**.
- Font: OpenSans Bold/Semibold có sẵn trong `Assets/Fonts/OpenSans` (đã kiểm tra đủ glyph tiếng Việt).
- Phong cách bảng điều khiển: nền xám đậm, viền/điểm nhấn cyan, nhấn phụ vàng. Không trang trí sau chữ nhỏ.

Thay đổi:

| Màn hình | Thay đổi |
|---|---|
| HUD | biểu tượng lõi + "x / y", tên khu, độ khó |
| Thẻ giới thiệu khu | hiện khoảng 2 giây đầu màn: tên khu + mục tiêu |
| Thắng màn / thắng game | "Đủ năng lượng" / "Đã về tới căn cứ" |
| Thua | "Mất kết nối" |
| Menu chính | tiêu đề mới; bỏ nút "Open Github Page" |
| Nút, joystick | xám đậm, điểm nhấn cyan |

## 4. Thứ tự triển khai

Mỗi bước xong đều demo được:

1. **Asset lõi + Level1 (màn thử):** robot, lõi năng lượng, robot địch, phế liệu rơi, Bãi phế liệu.
   Chụp ảnh gameplay để duyệt hình ảnh trước khi làm tiếp.
2. **Level2–4:** ba khu còn lại.
3. **Hiệu ứng + âm thanh.**
4. **UI.**
5. **Rà soát + build PC** (Linux; Windows nếu cài được module build).

## 5. Kiểm tra

- Mỗi bước: script biên dịch không lỗi, chạy batchmode không exception.
- Chụp ảnh từng màn bằng camera gameplay (Editor script tạm, không commit) để duyệt hình ảnh.
- **Playmode test** (Unity Test Framework, đã có trong `Packages/manifest.json`):
  - chạm lõi năng lượng thì `GameManager.gm.score` tăng;
  - va robot địch thì `Health` của người chơi giảm.
- Cuối cùng: build PC và chạy thử từ menu tới màn cuối.

## 6. Ngoài phạm vi

- Build Android/iOS, điều khiển cam cảm ứng.
- Cơ chế mới (cổng mở, trạm sạc tương tác, nhiều loại địch có AI khác nhau).
- Thiết kế lại đường đi hoặc địa hình các màn.
