# Robo lạc trên hành tinh lạ — Kế hoạch bàn giao triển khai

Ngày: 07/10/2026. Trạng thái: **ĐÃ DUYỆT PHẠM VI — SẴN SÀNG TRIỂN KHAI**. Tài liệu này chưa đồng nghĩa các thay đổi gameplay đã được thực hiện.

Tài liệu bàn giao cho teammate làm việc trên chính Unity project này. Đây là nguồn yêu cầu độc lập và duy nhất của đợt sửa; không cần bất kỳ file Markdown kế hoạch/spec nào khác. Các mục “bắt buộc” là phạm vi đã duyệt; thông số ghi “khởi điểm” là giá trị để thử nghiệm, không phải kết quả đã cân bằng. Teammate được tinh chỉnh các thông số này theo tiêu chí nghiệm thu và ghi lại kết quả trong tài liệu này.

### Hướng dẫn bắt đầu cho teammate

1. Đọc toàn bộ tài liệu, đặc biệt hiện trạng ở mục 2, hợp đồng kỹ thuật ở mục 10 và thứ tự công việc ở mục 11.
2. Dùng source, prefab, scene, asset, Packages và ProjectSettings trong cùng repository. Không tìm hoặc yêu cầu các kế hoạch Markdown cũ; chúng không thuộc gói bàn giao và có thể không có trên checkout của bạn.
3. Bắt đầu T0 để xác nhận Unity, asset LFS, thiết bị Android và baseline. Sau đó triển khai T1 và prototype L1 theo Gate B; không cần xin duyệt lại phạm vi đã chốt.
4. Phần bắt buộc gồm T0–T7. T8 (Overdrive) được chấp thuận là phần mở rộng sau khi T0–T7 đạt nghiệm thu; nếu lịch không đủ thì ghi rõ hoãn T8, không cắt phần bắt buộc.
5. Bàn giao source và build chơi được cùng bằng chứng kiểm thử. Chỉ tài liệu Markdown này cần đi kèm thay đổi; ghi hướng dẫn build, license mới, quyết định và kết quả nghiệm thu ngay tại đây thay vì phụ thuộc README hoặc tạo thêm tài liệu Markdown.

## 1. Phạm vi và nguồn quyết định

Cốt truyện: Robo dạng quả cầu bị lạc trên một hành tinh lạ. Robo đi qua bốn khu vực để thu đủ năng lượng khôi phục phi thuyền, lên tàu và thoát ra không gian.

Bản sửa gồm: thay hình năng lượng; cinematic kết thúc; thu nhỏ quái lớn và cải thiện AI; đổi quái nhỏ; mở rộng Level 1–2; thay nước chết bằng môi trường nguy hiểm phù hợp; joystick và vuốt camera Android; giảm mật độ năng lượng rơi. Boost tốc độ là hạng mục tùy chọn được đặc tả riêng.

Phạm vi đã duyệt cho phép mở rộng map, giữ nhân vật Robo, thêm phi thuyền và cinematic kết thúc. Nếu checkout có tài liệu thiết kế cũ với yêu cầu khác, dùng tài liệu này cho đợt sửa. Không cần đọc, sửa hoặc commit các file Markdown khác để triển khai kế hoạch.

Không mở rộng sang chiến đấu chủ động, cây kỹ năng, inventory, multiplayer hoặc level thứ năm. “Boss” trong tài liệu là quái lớn đang tồn tại, không mặc định bổ sung trận đấu boss có thanh máu. Giữ Unity 2020.3.19f1 và pipeline hiện tại; không cần nâng Unity hoặc chuyển Input System để hoàn thành phạm vi này.

## 2. Kết quả đọc code: hiện trạng và điểm cần sửa

Đã đọc script gameplay, prefab liên quan, cấu hình scene/build, generator asset và test. Chưa mở Unity để đo map, chạy gameplay hoặc profile Android; nhận định hình ảnh người chơi gặp lấy từ phản hồi của chủ dự án. Không dùng tài liệu cũ làm bằng chứng cho trạng thái code.

| Hạng mục | Bằng chứng trong repository | Hệ quả cho bản sửa |
|---|---|---|
| Năng lượng | `Tools/blender/make_core.py` tạo tinh thể với 4 thanh và 2 vòng kim loại; `AssetReplacer.cs` dùng EnergyCore cho Coin, Coin Bouncy và nhóm Sil/Vin Coins | Sửa cả nguồn tạo model, material, icon và các prefab sử dụng; chỉ đổi màu không loại được lồng |
| Nhặt item | `Treasure.cs` cộng score rồi Destroy; Coin và Coin Bouncy có `value = 1` | Giữ quy ước một lõi bằng một năng lượng; thêm chốt nhặt một lần và kiểm tra trạng thái chơi |
| Spawn | `SetupLevel.cs` tạo lưới 9/9/25/49 nguồn theo L1/L2/L3/L4; Easy chọn coin với xác suất khoảng 2/3, interval 1–3 giây mỗi nguồn | Phải giới hạn tổng toàn level, không chỉ tăng interval từng nguồn |
| Độ khó | Hard trong SetupLevel chỉ tạo enemy spawner; mục tiêu Easy là 5/10/20/40, Normal 5/15/40/60, Hard 5/25/60/100 | Không được giảm spawn rồi giữ mục tiêu lớn mà không kiểm tra khả năng hoàn thành; phải bảo đảm nguồn năng lượng ở mọi độ khó |
| Quái lớn | `Enemy - Monster.prefab` scale gốc 3; tốc độ 2/5/8, maxDist 35, minDist 3; `MonsterChaser` LookAt và dịch Transform trực tiếp | Kích thước, collider, tầm đánh và tốc độ cần chỉnh cùng nhau; thiếu tìm đường, gia tốc và chu kỳ đánh |
| Quái nhỏ | `Enemy - Crater.prefab` dùng `Chaser`, speed 4, minDist 0; Chaser cũng LookAt và dịch Transform | Đây là ứng viên chính cho creep mới; kiểm kê instance/override trong scene trước khi thay hàng loạt |
| Damage | Hai prefab quái bật damageOnCollision, damage 10; Player có healthPoints 1 | Animation đánh chưa điều khiển thời điểm gây sát thương; tiếp xúc hiện có thể chết ngay |
| Điều khiển | `BallUserControl` hỗ trợ joystick nhưng normalize vector và truy cập joystick không kiểm tra null | Mất độ lớn analog, kéo nhẹ cũng thành input tối đa; cần giữ biên độ input |
| Camera | `SmoothFollow` hiện đã có chuột phải để orbit/tilt và wheel zoom; vẫn lấy yaw mục tiêu từ quả cầu | Không coi camera là chưa có orbit; cần tách yaw camera khỏi chuyển động lăn, bổ sung touch và chống xuyên vật |
| Mobile | `FixedJoystick` dùng `new Camera()`, vị trí tâm tính một lần; `MobileCanvasControl` ẩn trên desktop | Cần sửa quy đổi tọa độ Canvas, quyền sở hữu pointer và safe area; kiểm cả joystick gắn trực tiếp trong scene |
| Biên map | `WaterDeathZone.prefab` có scale Y và collider size Y bằng 0 | Visual mới cần đi kèm thể tích trigger thực, không kế thừa collider dẹt chưa kiểm chứng |
| Kết thúc | `GameManager` ẩn player và hiện BeatLevel canvas; Level4 đánh dấu final; `UIButtonLevelLoad` có next theo buildIndex + 1 | Chưa có ending scene; cần route rõ ràng và tránh biến ending thành level chơi |
| Hạ tầng | Build settings có MainMenu + L1–L4; Timeline 1.4.8 và AI module đã có; `BuildScript` mới có BuildLinux | Dùng Timeline/NavMesh phù hợp Unity 2020; bổ sung build Android và ending vào danh sách build |
| Tests | Có GameplayTests và ThemeEditorTests; có test quái giết khi tiếp xúc và assert tên mesh/clip | Cập nhật test theo hợp đồng mới, không cố giữ hành vi va chạm cũ chỉ để test xanh |

Lưu ý baseline: bảng trên phản ánh source được đọc ngày 07/10/2026, không phải xác nhận runtime. Khi nhận việc, kiểm tra trạng thái checkout của bạn và không ghi đè/revert thay đổi có sẵn. Xác nhận asset LFS đã tải đủ, không còn file pointer thay cho model/texture, trước khi mở Unity/build. Nếu source khác bảng hiện trạng, ghi lại sai khác ảnh hưởng phạm vi tại mục 14 và xử lý theo yêu cầu đích của tài liệu.

## 3. Luồng chơi và tiến trình năng lượng

`MainMenu → chọn độ khó → Level1 → Level2 → Level3 → Level4 → Ending → màn hoàn thành`.

- Mỗi màn có mục tiêu riêng `Năng lượng: X/N`; một lõi cộng 1, không tiêu hao theo thời gian.
- Đủ N và còn sống: khóa nhận item/damage/input, kết thúc màn đúng một lần. L1–L3 hiện nút Đi tiếp; L4 tự chuyển sang Ending sau fade khoảng 0,5–1 giây.
- Đề xuất biểu diễn nhiên liệu tàu bằng 4 chặng: hoàn thành mỗi màn đóng góp 25%. HUD phụ hiển thị số chặng hoàn tất; trong màn hiện tại có thể nội suy theo X/N. Không cần inventory xuyên màn.
- `CampaignProgress` giữ trong phiên: difficulty, completedLevels và đóng góp đã xác nhận. Chốt đóng góp theo ID level, không cộng lại khi callback thắng chạy hai lần. Retry xóa score màn đang chơi, giữ các màn đã hoàn thành; New Game xóa toàn bộ tiến trình.
- Không đưa năng lượng dư sang màn sau. Không lưu campaign sau khi đóng ứng dụng trong phạm vi bắt buộc; thông báo rõ khi thoát về menu sẽ bắt đầu lượt mới.
- Bảo đảm đúng thứ tự bốn màn ở luồng người chơi. Mở trực tiếp Level4 trong Editor là chế độ debug, không được vô tình coi là đã hoàn thành campaign. Có cờ preview Ending chỉ dùng trong Editor để dựng cinematic.
- Khi damage chí mạng và lõi cuối xảy ra trong cùng bước physics, ưu tiên chết: tập hợp/giải quyết kết quả ở một nơi, dựa cả healthPoints lẫn trạng thái sống; không phụ thuộc thứ tự Update giữa Health và GameManager.
- Pause, intro, death, victory, transition phải khóa AI/spawn/pickup/input đồng nhất. Không dùng GameOver làm trạng thái chung cho cả thua và thắng như hiện tại.

## 4. Hình ảnh năng lượng và cơ chế xuất hiện

### 4.1. Asset bắt buộc

Chọn **lõi tinh thể năng lượng không khung**: khối low-poly cyan/trắng, phần lõi sáng, cạnh rõ, xoay chậm và nhấp nhô nhẹ. Loại hoàn toàn thanh/vòng kim loại bao quanh. Có thể có vài hạt sáng nhỏ; không phụ thuộc bloom để người chơi nhìn thấy.

- Chiều cao khởi điểm 0,5–0,7 đường kính Robo; nhìn rõ từ camera gameplay trên điện thoại.
- Mesh visual nằm trong child; trigger đơn giản trên root, không xoay/nhấp nhô collider theo mesh.
- Không gây damage, không đẩy Robo, không tiếp tục nảy lăn ra biển. Khi đáp xuống điểm hợp lệ, đứng tại vị trí nhặt; đường rơi là hiệu ứng có kiểm soát.
- Đồng bộ model/icon HUD/VFX pickup và nội dung hướng dẫn. Cập nhật `make_core.py`, assertion material cũ và logic AssetReplacer có thể bỏ qua prefab vì “already replaced”.
- Sửa cả Coin, Coin Bouncy, Sil Coins, Vin Coins và override trong bốn scene. Bảo toàn GUID/reference hoặc có migration tường minh.
- Tiêu chí đạt: không còn lồng đen trong gameplay/icon; dễ phân biệt với boost và quái ở cả vùng axit xanh lẫn lava đỏ; chạm một lõi chỉ tăng đúng 1.

### 4.2. Ngân sách spawn toàn màn

Tách spawn năng lượng khỏi spawn quái. `EnergySpawnDirector` là chủ sở hữu duy nhất của ngân sách energy; các nguồn cũ chỉ trở thành điểm đáp hoặc bị tắt chức năng sinh energy. Không chạy đồng thời hai hệ thống.

Thông số khởi điểm sau đây dùng cho Easy/Normal/Hard ở vòng cân bằng đầu; độ khó khác nhau trước hết ở AI và mật độ quái, không ở việc ép nhặt gấp nhiều lần:

| Level | Mục tiêu N | Có sẵn lúc bắt đầu | Trần đang tồn tại | Khoảng sinh một lõi toàn màn | Thời lượng lượt thắng mong muốn |
|---|---:|---:|---:|---|---|
| 1 | 6 | 3 | 5 | 5–7 giây | 60–90 giây |
| 2 | 10 | 4 | 6 | 5–7 giây | 90–120 giây |
| 3 | 14 | 5 | 7 | 4–6 giây | 100–150 giây |
| 4 | 18 | 6 | 8 | 4–6 giây | 120–180 giây |

Các thời lượng trên gồm di chuyển, tránh quái và tìm đường; không suy ra trực tiếp từ interval. Hướng giảm mục tiêu đã được duyệt; teammate điều chỉnh bằng số liệu playtest và ghi giá trị cuối tại mục 14.

Quy tắc triển khai:

1. Trần tính cả lõi đặt sẵn, đang rơi và đã đáp. Gom lại/giảm nhóm coin trang trí hiện có để không vượt ngân sách ngay từ đầu. Chỉ giữ đủ lõi để hoàn thành phần mục tiêu còn thiếu.
2. Danh sách điểm đáp được author theo map; có đất, không nằm trong collider vật cản, axit/lava hoặc sau đường không thể đi tới. Chọn điểm theo vùng và khoảng cách, tránh tập trung hết một góc.
3. Hiện dấu đáp trước khoảng 0,5 giây, rơi từ độ cao tương đối 3–5 m trên mặt đất trong 0,6–1 giây. Điểm đáp phải còn hợp lệ khi spawn. Không dùng mặc định Y=15/30 cho mọi địa hình.
4. Mỗi interval sinh tối đa một lõi; trần đầy thì chờ lượt sau, không tích backlog để xả hàng loạt khi player nhặt.
5. Lõi hợp lệ không tự hết hạn. Lõi rơi khỏi map/bị hủy ngoài dự kiến phải giải phóng slot và được bù. Nếu không còn lõi có thể tiếp cận trong 8 giây khi chưa đủ N, sinh bù ở một điểm dự phòng đã kiểm chứng; vẫn tôn trọng trần, dọn lõi hỏng trước.
6. Bộ đếm đăng ký/hủy đăng ký đúng một lần khi pickup, disable, destroy hoặc unload scene. Timer đứng yên khi pause; resume không sinh dồn.
7. Hard vẫn có director năng lượng. Không để random lựa chọn loại spawner quyết định việc màn có thể thắng hay không.

Tiêu chí đạt: quan sát 3 phút không tích lũy vô hạn, không vượt trần, không có lõi dưới đất/ngoài biên; mọi màn và độ khó đều có thể hoàn thành mà không cần boost.

## 5. Map và sự thống nhất bối cảnh

Đề xuất một hành tinh có biển axit, di tích công nghiệp và vùng địa nhiệt. Không chỉ đổi texture nước; đất, cây, vật cản, màu trời, quái và âm thanh phải cùng kể được bối cảnh.

| Level | Chủ đề đề xuất | Biên nguy hiểm | Thay đổi địa hình và quái |
|---|---|---|---|
| 1 | Bãi đáp hỏng trên đảo đá ngoại tinh | Biển axit vàng lục | Mở diện tích đi được khoảng 1,5 lần; có tuyến học điều khiển an toàn, đá thấp, mảnh vỡ tàu, creep thưa |
| 2 | Trạm khai thác bỏ hoang | Axit công nghiệp cùng hệ biển | Mở khoảng 1,4 lần; đường vòng quanh máy móc, 2–3 lối nối, creep và quái lớn có không gian rượt |
| 3 | Vùng địa nhiệt | Lava cam đỏ | Giữ layout chính, chỉnh đá bazan/cây khô/hơi nóng; quái dùng biến thể màu chịu nhiệt |
| 4 | Bãi phóng cũ | Axit và vách đá bao ngoài | Giữ layout chính, dấu chỉ dẫn về bãi phóng, cùng ngôn ngữ hình ảnh phi thuyền ở Ending |

Tỷ lệ là **diện tích đất đi được**, không phải nhân scale tuyến tính toàn scene. Đo diện tích nền/NavMesh kết nối trước và sau; không lấy bounds chứa cả biển làm diện tích chơi. Mức trên được chốt lại sau blockout, chưa phải số đo map hiện tại.

- L1–2 thêm vùng đất và đường đi có mục đích, không phóng to tất cả object. Đường chính rộng tối thiểu khoảng 3 đường kính Robo; đường cho boss phải đủ bán kính agent và chỗ né.
- Vùng bắt đầu an toàn ít nhất 5 giây đầu với input bình thường; lõi đầu nhìn thấy được. L1 giới thiệu joystick/vuốt rồi mới đặt áp lực truy đuổi.
- Cập nhật đồng bộ collision, điểm spawn, đường AI/NavMesh, minimap bounds, camera clipping, fog và lighting bake. Kiểm góc nhìn 360 độ để không lộ mặt sau trống hoặc mép mesh.
- Biên có dấu hiệu rõ: bờ đá tương phản, vệt ăn mòn/bọt axit hoặc nứt nóng. Không dùng màu giống lõi năng lượng làm tín hiệu duy nhất.
- Tách `HazardVisual` và thể tích `DeathZone` có chiều dày thật; giữ root physics scale hợp lệ. Chạm axit/lava là game over; kill-plane dưới map bắt trường hợp lọt collider.
- UI nguyên nhân chết: “Robo rơi xuống biển axit” / “Robo chạm dung nham”. Không dùng lại chữ “nước” ở level đã đổi.
- Android: ưu tiên shader đơn giản có UV flow/emission, hạn chế transparency chồng lớp, không yêu cầu phản xạ realtime.

Tiêu chí đạt: có ảnh top-down trước/sau chứng minh mở rộng L1–2; chơi được toàn bộ tuyến với camera tự do; không có đảo năng lượng không thể tới; chạm mọi đoạn biên kiểm mẫu đều chết đúng một lần.

## 6. Quái lớn, creep và AI truy đuổi

### 6.1. Asset và kích thước

- Boss: khởi điểm giảm kích thước visual xuống 60–65% hiện tại, hướng tới chiều cao khoảng 2–2,5 đường kính Robo. Kiểm renderer bounds thực vì scale gốc 3 chưa thể hiện toàn bộ tỷ lệ model/scene override.
- Chuẩn hóa root scale về 1 nếu migration an toàn; chỉnh visual child và tính lại collider, agent radius/height, tâm đứng, tầm đánh theo đơn vị world. Không chỉ thu mesh rồi để collider cũ.
- Creep: đề xuất robot bọ trinh sát low-poly 4 chân, thân thấp, silhouette khác boss và Robo; kích thước khoảng 0,7–1 đường kính Robo, mắt đỏ/cam để khác lõi cyan. Thay hình crate hiện tại và hành vi đi trên đất; không tiếp tục cho quái mặt đất “bay thẳng” từ spawner trên cao.
- Chọn asset có license dùng/phân phối được, có idle/run/attack hoặc dựng chuyển động đơn giản đáp ứng cùng tín hiệu. Giao bảng nguồn, license, mesh/material và preview trước khi import hàng loạt. Chưa khẳng định repository đã có sẵn robot bọ/phi thuyền phù hợp.

### 6.2. Cơ chế di chuyển

Ưu tiên Unity NavMesh bake theo từng scene, có agent type/radius phù hợp boss và creep. Spike trên map có dốc và vật cản trước khi nhân ra bốn scene; không mặc định cài package AI Navigation đời mới vào Unity 2020.

- Một `EnemyBrain` quản lý state; `EnemyMotor` là nơi duy nhất điều khiển vị trí. Khi dùng NavMeshAgent, bỏ dịch Transform từ Chaser/MonsterChaser và không để Rigidbody động/animation root motion cùng kéo root.
- Trạng thái: Idle/Patrol → Chase → Windup → Strike → Recover; thêm Return khi mất dấu và Disabled khi hết gameplay.
- Cập nhật destination mỗi 0,15–0,25 giây, lệch nhịp giữa các enemy; quay hướng và animation mỗi frame. Không tính lại full path cho tất cả quái mỗi frame.
- Đi theo vận tốc thực trên mặt đất, tăng/giảm tốc có giới hạn, quay yaw dần. Không LookAt theo Y khiến quái cúi/ngửa khi Robo nhảy.
- Attack chỉ khi trong khoảng cách/góc hợp lệ và không có vật cản; không đánh xuyên tường chỉ vì Euclidean distance nhỏ.
- Nếu player ngoài NavMesh/đang nhảy, dùng điểm mặt đất hợp lệ gần vị trí cuối; không cho agent đi vào axit/lava. Path partial/invalid chuyển chờ/tìm lại, không xuyên tường hoặc teleport tới player.
- Bị kẹt: sau khoảng 1,5 giây không tiến triển, repath/đổi điểm đến cục bộ; sau 3 giây vẫn kẹt thì Return tới điểm hợp lệ, không teleport trong tầm nhìn. Dùng hysteresis bán kính phát hiện/mất dấu để tránh đổi state liên tục.
- Enemy spawn trên điểm NavMesh đã kiểm chứng, có báo hiệu khoảng 0,7 giây, cách Robo tối thiểu khoảng 6 đường kính và không chặn ngay spawn người chơi.

Thông số khởi điểm: đo tốc độ chạy ổn định của Robo trên nền phẳng là V; đặt giới hạn tốc độ tuyến tính cho Robo trong Ball trước khi cân bằng AI. Boss Easy/Normal/Hard lần lượt khoảng 0,70/0,85/1,00 V; creep 0,80/0,95/1,05 V. Đo lại để bảo đảm tốc độ tiếp cận nhanh hơn bản hiện tại trong tình huống tương ứng, nhưng người chơi vẫn có cửa né nhờ báo đòn và địa hình. Không tăng tốc tùy tiện thành truy đuổi không thể thoát.

Trần enemy khởi điểm toàn map theo L1–L4: 3/5/7/9, gồm tối đa 1/1/2/2 boss. Normal dùng trần này; Easy giảm creep khoảng 1 mỗi màn, Hard tăng tối đa 1 mỗi màn sau profile. Đếm cả quái đặt sẵn và sinh động; tắt nguồn cũ không tuân thủ trần. L1 chỉ kích hoạt boss sau đoạn học điều khiển.

### 6.3. Đòn đánh có thể đọc và né

| Pha | Boss khởi điểm | Creep khởi điểm | Luật |
|---|---|---|---|
| Windup | 0,55 giây | 0,35 giây | Dừng truy đuổi, báo đòn rõ, chốt hướng; không tự xoay bám player suốt đòn |
| Strike | cửa sổ 0,15 giây | cửa sổ 0,10 giây | Một lần kiểm tra hit, trong tầm/góc/line of sight, mỗi mục tiêu nhận tối đa một hit |
| Recover | 0,8 giây | 0,7 giây | Không gây damage, sau đó mới Chase/đánh tiếp |

Giữ luật một đòn trúng thì thua ở bản đầu. **Tắt damageOnCollision/damageOnTrigger gây sát thương thụ động trên thân quái** khi dùng đòn mới; giữ collider ngăn xuyên thân. Các hazard môi trường giữ luật chết riêng. Logic hit do combat state quản lý, animation event chỉ đồng bộ khi có; không để thiếu clip làm quái bất tử hoặc đánh liên tục.

Blend idle/run/attack khoảng 0,1–0,2 giây, chỉ chuyển clip khi state đổi, điều chỉnh tốc độ run animation theo vận tốc. Chỉ dùng một hướng animation (Legacy hiện có hoặc Animator đã migrate), không bật đồng thời hai hệ điều khiển.

Tiêu chí đạt: quái vòng qua vật cản chữ U, không rung tại tầm đánh, không đánh xuyên tường, không chạm thân chết trước báo đòn; né khỏi tầm đúng lúc thì đòn hụt. Pause/chết/thắng không để đòn đang chờ gây hit bất ngờ.

## 7. Android: joystick, camera và cảm giác lăn

- Màn hình ngang; hỗ trợ ít nhất 16:9, 20:9 và tablet 4:3. HUD/nút/joystick nằm trong safe area; CanvasScaler theo kích thước tham chiếu, không dùng pixel tuyệt đối cho mọi máy.
- Joystick cố định góc trái dưới, vùng chạm khoảng 20–25% chiều cao màn hình. Dead zone khởi điểm 0,1; vector giữ biên độ analog, clamp magnitude ≤1, không tăng tốc đường chéo.
- Vuốt vùng trống nửa phải xoay camera thứ ba: yaw 360 độ, pitch giới hạn khởi điểm 15–65 độ. Độ nhạy khởi điểm vuốt hết chiều rộng vùng nhìn tương đương 180 độ, có thanh chỉnh độ nhạy.
- Mỗi thao tác giữ finger/pointer ID từ Down tới Up/Cancel. Ngón bắt đầu ở joystick không chuyển thành camera khi vượt giữa màn; ngón camera không điều khiển joystick. Nút pause/nhảy nhận touch riêng và có ưu tiên hơn vùng camera.
- `FixedJoystick` dùng RectTransformUtility và đúng event camera của Canvas; bỏ `new Camera()`, tính lại tọa độ khi layout đổi. Reset khi disable, mất focus, pause và đổi scene.
- `BallUserControl` lấy input trong Update, truyền vào physics trong FixedUpdate; null-safe khi không có joystick. Dùng forward/right chiếu xuống mặt phẳng camera; giữ độ lớn input sau phối hợp hai trục.
- `Ball` giữ cơ chế lăn bằng lực/torque, bổ sung trần tốc độ ngang, tăng tốc/phanh có cấu hình; bật Rigidbody interpolation nếu kết quả kiểm tra phù hợp. Không clamp mất vận tốc rơi/nhảy. Mục tiêu ban đầu giảm trượt sau nhả joystick, vẫn giữ cảm giác quả cầu có quán tính.
- Đề xuất giữ nhảy đang có và thêm nút Android, đồng thời xác minh thực sự cần cho tuyến map; không thay đổi khả năng qua địa hình giữa desktop/mobile. Nhảy xử lý theo lần nhấn, không lặp vô hạn khi giữ nút.
- `ThirdPersonOrbitCamera` riêng cho Main Camera: lưu yaw/pitch độc lập với góc xoay Robo, follow ở LateUpdate, SphereCast từ pivot tới vị trí camera để tránh xuyên tường. Bỏ qua player, collectible, trigger; có camera obstacle mask riêng.
- Giữ minimap cố định bằng SmoothFollow hoặc follower riêng; không nhận vuốt/chuột orbit/zoom của Main Camera. Kiểm mọi instance SmoothFollow, không sửa đồng loạt vô điều kiện.
- Desktop giữ WASD/phím mũi tên, chuột phải orbit và wheel zoom; các input camera cũng phải bị khóa khi intro/pause/death/victory. Không lấy mô phỏng chuột làm bằng chứng multitouch chạy đúng.

Tiêu chí đạt: chạy bằng ngón trái và nhìn bằng ngón phải liên tục 60 giây không cướp touch, thêm nhảy/pause không khóa joystick; kéo nhẹ đi chậm; nhả/cancel không kẹt input; cùng cử chỉ có cảm giác gần nhau ở 30/60 FPS. Pointer delta đã là quãng dịch chuyển, không nhân deltaTime thêm lần nữa.

## 8. Ending: cảnh kết thúc, không phải level mới

Tạo `Assets/Scenes/Ending.unity`, không có mục tiêu thu thập, enemy hay điều khiển gameplay. Dùng Timeline/PlayableDirector đã có trong dự án, một Robo diễn hoạt riêng và phi thuyền low-poly cùng phong cách. Không phụ thuộc player gameplay vốn bị GameManager ẩn khi thắng.

Storyboard đề xuất 22–28 giây:

| Thời gian | Hình ảnh/hành động | Âm thanh/UI |
|---|---|---|
| 0–3 giây | Fade từ L4, toàn cảnh phi thuyền hỏng ở bãi phóng | Gió, tiếng điện yếu |
| 3–8 giây | Robo lăn tới điểm tiếp năng lượng, camera theo chếch sau | Tiếng lăn, nhạc bắt đầu mở |
| 8–13 giây | Luồng sáng từ Robo tới cổng nạp; bốn cụm đèn trên tàu lần lượt sáng | Âm nạp tăng dần; nhiên liệu 100% |
| 13–17 giây | Hệ thống tàu phục hồi, cửa mở; Robo lăn vào, cửa đóng | Tiếng cơ khí, động cơ khởi động |
| 17–23 giây | Tàu cất cánh rời bãi, camera kéo xa | Động cơ, nhạc cao trào |
| 23–28 giây | Góc ngoài không gian nhìn hành tinh và tàu bay đi | “Robo đã thoát khỏi hành tinh. Hành trình tiếp tục.” |

- Cảnh nạp phải cho thấy nguồn năng lượng từ Robo và tàu hoạt động lại; cảnh lên tàu phải rõ để không tạo cảm giác tàu bỏ Robo lại.
- Dùng chuyển cảnh/skybox để biểu diễn không gian trong cùng Ending, không cần mô phỏng bay ra toàn bộ khí quyển.
- Nút Bỏ qua xuất hiện sau 1 giây; click/touch và phím Escape tương đương. Skip và kết thúc tự nhiên gọi cùng một hàm CompleteEnding có chốt chạy một lần.
- Màn hoàn thành có Chơi lại từ đầu và Về menu, không có Đi tiếp tới Level5. New Game reset campaign và thời gian/input; không để Timeline/AudioSource tồn tại sang gameplay.
- Android Home/mất focus: tạm dừng Timeline/audio, tiếp tục đúng vị trí khi quay lại; skip sau resume vẫn hoạt động. Chuyển cảnh luôn chuẩn hóa timeScale, không thừa trạng thái pause từ L4.
- Cập nhật build settings và route bằng scene name/config rõ ràng; không dựa vào buildIndex + 1 cho logic campaign.

Tiêu chí đạt: hoàn thành bốn màn dẫn tới Ending đúng một lần; có đủ lăn tới tàu → nạp → lên tàu → cất cánh → không gian; xem hết hoặc skip đều tới cùng màn hoàn thành.

## 9. Boost tốc độ — tùy chọn sau phần bắt buộc

Đề xuất chỉ thêm một loại ở vòng đầu: **Overdrive** màu vàng, icon tia sét; tự kích hoạt khi nhặt, không cộng điểm năng lượng.

- Tốc độ tối đa ×1,3 và lực tăng tốc ×1,15 trong 5 giây; có icon đếm ngược. Thời gian dùng gameplay time, đứng yên khi pause.
- Nhặt lại làm mới thời gian lên 5 giây, không cộng dồn multiplier. Hết hiệu ứng trả thông số gốc, giảm tốc mượt để tránh giật.
- Tối đa 1 boost sống trên map; đề xuất xuất hiện mỗi 25–35 giây từ L2, ở điểm an toàn không sát biên. Không chiếm ngân sách energy hoặc cần thiết để thắng.
- Retry/chết/chuyển màn xóa hiệu ứng. Không áp multiplier lặp vào thông số đã nhân ở frame trước.
- Chỉ triển khai sau khi tốc độ thường và AI đã cân; không thêm shield/magnet đồng thời. Nếu lịch thiếu, hoãn toàn bộ mục này, không cắt Android/Ending/AI.

## 10. Thiết kế kỹ thuật và điểm tích hợp

Tên component sau là đề xuất để phân công, không bắt buộc giữ nguyên tên nếu teammate có cấu trúc tương đương:

| Module | Hợp đồng | File hiện tại cần tích hợp |
|---|---|---|
| LevelConfig (ScriptableObject) | level ID, scene kế, N, spawn interval/cap, enemy budget theo difficulty, theme | SetupLevel, GameManager, Zones |
| CampaignProgress | BeginRun, CompleteLevel một lần/ID, CanPlayEnding, Reset; không giữ reference scene | GameDifficulty/GameSettings, UIButtonStartGame, UIButtonLevelLoad |
| GameFlow | Trạng thái intro/playing/paused/dead/levelComplete/transition; mọi hệ gameplay đọc cùng nguồn | GameManager, Health, UI pause/resume |
| EnergySpawnDirector | Đăng ký item, chọn điểm, kiểm ngân sách và bổ sung khi nguồn hỏng | SpawnGameObjects, SetupLevel, Treasure |
| EnemyBrain/Motor/Attack | State, navigation, báo đòn, hit một lần; EnemyConfig riêng theo loại/độ khó | MonsterChaser, Chaser, EnemyMove, Damage |
| TouchInputRouter + OrbitCamera | Move vector, look delta, jump pressed, reset; pointer ownership | Joystick, FixedJoystick, BallUserControl, SmoothFollow, MobileCanvasControl |
| EndingController | Play, Skip, Complete một lần; Timeline không điều khiển campaign trực tiếp | GameManager final branch, scene route |
| HazardVolume | Chết một lần + nguyên nhân; visual tách physics | WaterDeathZone, Damage/Health |

Scene/prefab: mở rộng terrain L1–2; theme/hazard cả bốn; thay Coin/Coin Bouncy/nhóm coin, Enemy - Monster/Crater; cập nhật MainCanvas, Mobile Canvas, Fixed Joystick, minimap; thêm Ending, config và NavMesh data.

Editor tools phải hiểu config/theme mới: AssetReplacer, ZoneDresser, UiTheme và EffectsTheme không được chạy lại rồi phục hồi asset/nhãn cũ. Các vòng lặp trên mọi build scene phải bỏ qua Ending/MainMenu nếu chỉ áp dụng cho level gameplay. Ghi hướng dẫn build và bảng nguồn/license asset mới tại mục 14 của tài liệu này. Giữ nguyên các file license gốc đi kèm asset; không thay thế chúng bằng bảng tóm tắt.

## 11. Kế hoạch giao việc, phụ thuộc và điểm duyệt

Ước lượng cho người đã quen Unity, một ngày công khoảng 6–8 giờ tập trung. Đây là dự toán, chưa biết tốc độ teammate, chất lượng asset mua/tải và thiết bị Android. Tổng cơ sở khoảng **17–25 ngày công**, cộng dự phòng 20%; boost thêm 0,5–1 ngày công. Nhóm 3 người có thể lập lịch khoảng 2–3 tuần làm việc nếu chia độc lập và tích hợp đều; không ép vào kế hoạch bốn ngày cũ.

| ID | Gói việc và đầu ra giao nộp | Owner phù hợp | Phụ thuộc | Ngày công |
|---|---|---|---|---:|
| T0 | Mở đúng Unity, xác nhận LFS, video baseline bốn màn, đo V/kích thước/diện tích, audit override và thử build APK từ baseline chưa sửa gameplay | Tech lead + QA | Nhận project và tài liệu này | 1 |
| T1 | LevelConfig, campaign/game flow, route scene, ưu tiên chết/thắng và hợp đồng pause | Gameplay | T0 | 1–2 |
| T2 | Joystick analog, pointer router, orbit/collision camera, safe area, tuning Ball; video Android hai ngón | Gameplay/mobile | T1 | 2–3 |
| T3 | Model/icon energy, director và điểm đáp, bỏ nguồn sinh trùng; log số item sống và test đủ nguồn mọi difficulty | Gameplay + art | T1; dùng blockout T4 | 1,5–2 |
| T4 | Blockout mở rộng L1–2, palette bốn map, acid/lava volume, spawn points, minimap và bake cuối | Level artist | T0; hợp đồng T3/T5 | 3–4 |
| T5 | Spike navigation, boss nhỏ lại, creep mới, enemy budget, state/attack và animation; video vượt vật cản | AI + art | T1; blockout T4 | 3–4 |
| T6 | Asset tàu, storyboard, Ending/Timeline/skip/focus, nối từ L4 | Cinematic/art + gameplay | T1, palette T4 | 2–3 |
| T7 | Build Android chính thức, cân gameplay trên máy thật, regression, profile, sửa lỗi và tài liệu bàn giao | Integrator + QA | T2–T6 | 3–5 |
| T8 | Boost Overdrive và kiểm thử reset/duration/multiplier | Gameplay | T2/T3/T5 ổn định | 0,5–1 tùy chọn |

Trình tự và review:

1. **Gate A — phạm vi đã duyệt:** triển khai theo các quyết định tại mục 13. Không cần chờ duyệt lại spec; bắt đầu T0, ghi owner/lịch/thiết bị tại mục 14. Chỉ trao đổi lại khi cần thay đổi phạm vi hoặc có trở ngại làm không đạt tiêu chí bắt buộc.
2. **Gate B — prototype L1:** đưa joystick/camera, một boss/creep, energy director và biên axit vào blockout L1. Chủ dự án chơi thử cảm giác điều khiển và né đòn trước khi nhân ra bốn màn.
3. **Gate C — duyệt hình ảnh:** ảnh energy trên map, kích thước boss cạnh Robo, creep, L1–2 top-down, palette bốn map, animatic Ending. Chốt asset trước khi polish/bake hàng loạt.
4. **Gate D — content complete:** chơi liên tục MainMenu → 4 màn → Ending trên desktop và Android; tất cả mục bắt buộc có mặt, không còn placeholder ảnh hưởng nhận diện.
5. **Gate E — bàn giao:** đạt ma trận QA, đính kèm APK, video và log test/profile. Chủ dự án duyệt cảm giác chơi cuối.

Quy tắc phối hợp: chỉ một owner sửa mỗi `.unity` tại một thời điểm; programmer ưu tiên prefab/component, level artist giữ scene, integrator nối cuối. Các vai trò trong bảng là trách nhiệm, không yêu cầu phải có đủ từng người; một teammate có thể thực hiện tuần tự. Commit `.meta` và NavMesh/lighting data cần thiết; không commit Library/Temp. Chọn file để stage rõ ràng, không dùng stage toàn bộ khi working tree có tài liệu/thay đổi ngoài phạm vi. Không xóa file Markdown khác trên máy chỉ vì chúng không thuộc gói commit. Mỗi gói giao có danh sách file, cách test, bằng chứng và các lỗi còn lại. Không chỉ gửi screenshot thay cho build chơi được.

## 12. Kiểm thử và tiêu chí bàn giao

### Tự động: EditMode/PlayMode

- Config bốn level tồn tại, scene route hợp lệ, Ending nằm trong build và không có next level ngoài ý muốn.
- Pickup một lần dù player có nhiều collider; score đúng; không nhận sau chết/thắng; case damage chí mạng + lõi cuối ưu tiên chết.
- Director tính đúng placed/in-flight/landed, không vượt cap, không spawn backlog, xử lý destroy/retry/unload và không thiếu nguồn ở Hard.
- Campaign không cộng đôi level, retry giữ tiến trình trước, New Game reset, đủ bốn level mới vào ending ở luồng runtime.
- Attack gây đúng một hit trong tầm, hụt khi né, không xuyên vật cản; pause/disable hủy hoặc đóng băng đúng state. Thay test `PatrolRobotKillsPlayerOnContact` bằng các case này.
- Touch router giữ pointer độc lập và reset trên cancel/focus/scene; mapping analog không normalize mất biên độ. Kiểm UI multitouch thực tế vẫn bắt buộc.
- Ending skip/finish chỉ complete một lần, trở về menu/chơi lại không còn camera/audio/timeScale cũ.
- Cập nhật assertions tên mesh/clip/theme nếu đổi asset có chủ ý; giữ các kiểm tra reference, collider, lighting hợp lệ. Chạy lại test cũ còn phù hợp để tìm regression.

### Chơi thử thủ công

| Nhóm | Ca bắt buộc | Điều kiện đạt |
|---|---|---|
| Campaign | 4 level × 3 độ khó; retry mỗi màn; chạy trọn campaign ít nhất một lần mỗi difficulty | Đều có đủ nguồn energy và route đúng, không soft-lock |
| Energy | Full cap, nhặt liên tục, bỏ mặc 3 phút, phá một item ngoài dự kiến | Không vượt trần, không thiếu lõi vĩnh viễn, không tụ hàng trăm object |
| AI | Tường chữ U, dốc, góc hẹp, player nhảy, sát biên, 3 quái truy đuổi đồng thời | Không xuyên tường/rơi khỏi NavMesh/rung liên tục/đánh xuyên vật |
| Android input | Hai ngón + nút nhảy, kéo ra khỏi vùng, nhấc một ngón, notification/Home/resume | Không cướp pointer, không tự chạy/xoay sau khi nhả |
| Camera | Orbit 360°, pitch min/max, đứng sát tường/dưới prop, mất target | Không xuyên đất, không mất player kéo dài, không exception |
| Hazard | Rơi ở nhiều đoạn bờ, khi đang boost, ngoài mesh, trigger liên tục | Chết một lần đúng nguyên nhân; retry hoạt động |
| Ending | Xem hết, skip đầu/giữa/cuối, spam skip, background/resume | Cùng một màn hoàn thành, không double load, thấy Robo lên tàu |
| UI | 16:9, 20:9 có notch, tablet 4:3; font tiếng Việt | Không che HUD/nút, safe area đúng, chữ không lỗi dấu |

Android build nghiệm thu: APK development để profile và APK chơi thử, ARM64/IL2CPP nếu toolchain của Unity 2020 đã được xác nhận ở T0; xác nhận Android Build Support/SDK/NDK/JDK trước khi ước lượng build cuối. Mốc OS đề xuất Android 9+, API target chọn theo SDK tương thích và mục đích cài thử; phát hành Play Store và nâng SDK theo chính sách store là phạm vi riêng.

Chốt ít nhất hai máy thực ở T0: một máy tầm trung làm thiết bị nghiệm thu chính, một máy yếu hơn để kiểm low setting; ghi model/SoC/RAM/OS/resolution vào báo cáo. Trên máy chính, mục tiêu 60 FPS nếu khả thi, điều kiện tối thiểu profile khởi điểm median ≥30 FPS và p95 frame time ≤40 ms trong 10 phút ở cảnh đông nhất sau warm-up; không có spike trên 100 ms lặp lại do spawn/repath. Loading scene đo riêng. Máy yếu dùng chất lượng thấp và ghi kết quả thực, không tuyên bố hỗ trợ mọi Android.

Theo dõi CPU/GPU frame time, số enemy/energy sống, GC allocation, bộ nhớ và nhiệt. Vòng Update gameplay mới không tạo allocation thường xuyên; nếu Instantiate/Destroy gây spike thì pool theo budget đã đo. Kiểm 10 vòng retry/chuyển scene không tăng object/memory không giới hạn. Không tăng số đèn/particle cho từng pickup để bù shader chưa đúng.

Gói bàn giao cuối: source + prefab/scene/config + metadata; APK; video Android điều khiển và Ending; kết quả EditMode/PlayMode; bảng playtest thời gian thắng/thua theo difficulty; profile thiết bị. Hướng dẫn build, bảng asset/license, thông số cuối và đường dẫn bằng chứng được ghi tại mục 14 của chính tài liệu này. APK/video/report có thể giao dưới dạng artifact riêng, không bắt buộc commit file build dung lượng lớn vào Git.

## 13. Các quyết định đã duyệt để triển khai

1. Dùng tinh thể cyan không lồng; creep robot bọ; boss giữ nhận diện robot nhưng nhỏ hơn.
2. L1–2 dùng axit, L3 lava, L4 bãi phóng/axit; mở diện tích đi được L1 khoảng 1,5 lần, L2 khoảng 1,4 lần sau đo baseline.
3. Mục tiêu thử 6/10/14/18 cho cả ba difficulty; độ khó tăng bằng hành vi và áp lực quái; giữ một đòn trúng thì thua nhưng có báo đòn và né được.
4. Android ngang, joystick trái + vuốt phải, yaw/pitch có giới hạn, giữ nút nhảy; desktop giữ cách điều khiển chuột hiện có.
5. Ending riêng 22–28 giây với Robo thực sự lên tàu; không thêm level thứ năm; campaign lưu trong phiên.
6. Boost Overdrive triển khai sau khi phần bắt buộc đạt nghiệm thu; có thể hoãn nếu lịch không đủ và phải ghi rõ trong bàn giao. Không làm điều kiện hoàn thành game.

Các tỷ lệ, thời lượng và thông số gameplay nêu trên là baseline đã duyệt để thử nghiệm. Teammate tự tinh chỉnh trong cùng thiết kế nhằm đạt tiêu chí nghiệm thu, không phải xin xác nhận cho từng giá trị số. Thay đổi cốt truyện, bỏ hạng mục bắt buộc, thêm level chơi hoặc thay đổi luật thắng/thua cần trao đổi với chủ dự án.

## 14. Nhật ký triển khai và bàn giao — teammate cập nhật tại đây

Không để trống các phần liên quan khi đánh dấu bản sửa hoàn tất. Chưa có dữ liệu thì ghi “Chưa đo/Chưa chạy”, không ghi đạt dựa trên suy đoán.

### 14.1. Baseline và phân công

- Commit baseline / branch triển khai: ba66683 / robo-escape.
- Unity đã mở project thành công / tình trạng LFS: Unity 2020.3.19f1 mở project bằng batchmode thành công; git lfs fsck OK, không còn pointer.
- Owner T0–T7, người tích hợp và lịch dự kiến: Chưa phân công.
- Thiết bị Android chính/phụ (model, SoC, RAM, OS, độ phân giải): Chưa chọn — cần chủ dự án cung cấp 2 máy.
- Sai khác source so với mục 2 và cách xử lý: Khớp mục 2. Thêm: Zones.cs giữ tên khu (thay bằng LevelConfig ở T1); chưa có pause UI (chỉ UIButtonResumeGame).
- Số đo ban đầu (V của Robo, kích thước boss/creep, diện tích đi được L1–2): Robo không có tốc độ ổn định (Ball dùng AddForce 25 N, khối lượng 1,3, drag 0,1, UseTorque tắt; jumpPower 0 nên nhảy đang tắt) — giữ full input từ đứng yên trên nền phẳng: 1 giây 11,6 m/s, 2 giây 19,6 m/s, 4 giây 33,1 m/s; Sau T2: Ball có trần tốc độ ngang 9 m/s (tăng tốc 25 N, phanh 8 m/s² khi nhả, không chặn vận tốc rơi) → **V = 9,0 m/s** (đạt sau < 1 giây). Boss `Enemy - Monster` (root scale 3): bounds 13,9 × 7,6 × 5,0 (cao ≈ 7,6 đường kính Robo). Creep `Enemy - Crater`: 1,47 × 1,53 × 1,47. Diện tích đi được (lưới 1 m, dốc < 45°): L1 = 1021 m², L2 = 2237 m². Audit override (instance / override ngoài transform): L1 Coin 5/1, WaterDeathZone 1/0; L2 Coin 35/0; L3 Coin 119/0, Sil 1, Vin 1, Monster 3/5; L4 Coin 236/0, Sil 1, Vin 1, Monster 8/0; Crater không có instance đặt sẵn (chỉ sinh từ spawner).

### 14.2. Tiến độ và bằng chứng

| Gói | Trạng thái | Owner | Commit/file chính | Bằng chứng, lỗi còn lại |
|---|---|---|---|---|
| T0 Baseline | Đang làm | Claude | BuildScript.cs, BaselineProbe.cs, BaselineProbes.cs | APK dev baseline build OK; số đo ở 14.1. Chưa có: video baseline 4 màn, thiết bị Android (cần chủ dự án). |
| T1 Flow/config | Xong | Claude | Assets/Scripts/Flow/*, GameManager.cs, Treasure.cs, Resources/Levels, Scenes/Ending.unity | EditMode 30/30, PlayMode 17/17 (gồm chết-trước-thắng, nhặt 1 lần, route đủ 4 màn → Ending, Level4 mở trực tiếp → menu). Ending hiện chỉ có màn hoàn thành (cinematic ở T6); pause UI/nút ở T2. |
| T2 Android/control | Xong (chờ thử máy thật) | Claude | Ball.cs, BallUserControl.cs, FixedJoystick.cs, Scripts/Controls/*, Editor/ControlSetup.cs, 4 level scene | EditMode 41/41, PlayMode 28/28 (trần tốc độ, phanh, rơi không bị chặn, joystick analog + giữ pointer, vùng vuốt bỏ qua pointer khác, reset khi mất focus, camera dừng trước vật cản, tạm dừng/tiếp tục). Chưa có: video Android hai ngón (chưa có thiết bị). Nhảy vẫn tắt như baseline nên không thêm nút nhảy. |
| T3 Energy | Chưa làm | — | — | — |
| T4 Map/theme | Chưa làm | — | — | — |
| T5 Enemy/AI | Chưa làm | — | — | — |
| T6 Ending | Chưa làm | — | — | — |
| T7 Build/QA | Chưa làm | — | — | — |
| T8 Overdrive | Sau phần bắt buộc | — | — | Ghi rõ triển khai hoặc hoãn |

### 14.3. Hướng dẫn build độc lập

Baseline cần Unity **2020.3.19f1**. Mở project bằng Unity Hub sau khi cài Git LFS và tải đủ asset, sau đó mở `Assets/Scenes/MainMenu.unity` để chơi. Build settings hiện có MainMenu, Level1–4; bản hoàn thành phải thêm Ending và có đủ scene được enable.

Teammate bổ sung quy trình Android đã chạy thành công, không yêu cầu người nhận tra README:

- Unity Android Build Support, SDK/NDK/JDK và đường dẫn/cách cấu hình: cài bằng `unityhub --headless install-modules -v 2020.3.19f1 -m android android-sdk-ndk-tools android-open-jdk --cm` (Hub đã đăng ký editor 2020.3.19f1). Kết quả: OpenJDK 1.8 (adoptopenjdk), SDK platforms android-29/30 + build-tools 30.0.2 + platform-tools, NDK r19 (19.0.5232133) trong `<Unity>/Editor/Data/PlaybackEngines/AndroidPlayer/{OpenJDK,SDK,NDK}`; Unity tự nhận, không cần EditorPrefs.
- Minimum/target API, scripting backend, architecture, graphics API: min API 28 (Android 9), target Auto, IL2CPP, ARM64, graphics API mặc định (Auto). Màn hình ngang (landscape left/right).
- Cách build bằng Editor và tên entry point nếu bổ sung build script: Tools > Robo Lac Loi > Build Android (development/test), hoặc batch `unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod BuildScript.BuildAndroidDevelopment` (hoặc `BuildAndroidTest`). KHÔNG dùng `-nographics`. Ký APK: đặt biến môi trường `ANDROID_KEYSTORE`, `ANDROID_KEYSTORE_PASS`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASS`; không đặt thì dùng `~/.android/debug.keystore` (phải là JKS đọc được bởi JDK 8). BuildScript xoá thiết lập ký sau khi build để không lưu đường dẫn/mật khẩu vào ProjectSettings. Sau khi build Android, chuyển lại desktop bằng `-buildTarget Linux64 -executeMethod BuildScript.BuildLinux`.
- Package ID/version, vị trí APK development và APK chơi thử: com.hongandev.game3dd, bundleVersion hiện tại; `Builds/Android/RoboLacLoi-dev.apk` (development, ~51 MB, build baseline 08/10/2026 OK) và `Builds/Android/RoboLacLoi.apk` (chơi thử). Thư mục `Builds/` không commit.
- Cách chạy EditMode/PlayMode tests và nơi lấy report: đóng Unity Editor, chạy `unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults <file>.xml` (hoặc `PlayMode`); report là file XML NUnit tại đường dẫn `-testResults`. Probe baseline T0: thêm `-testFilter BaselineProbes` (PlayMode) và `-executeMethod BaselineProbe.Run` (không `-nographics`).
- Không ghi keystore password, token hoặc thông tin bí mật vào tài liệu/repository.

### 14.4. Asset, cân bằng và nghiệm thu

| Asset thêm/thay | Nguồn/tác giả | License và file license gốc | Đường dẫn trong project |
|---|---|---|---|
| Lõi năng lượng | Chưa cập nhật | Chưa ghi | Chưa ghi |
| Creep | Chưa chọn | Chưa ghi | Chưa ghi |
| Phi thuyền | Chưa chọn | Chưa ghi | Chưa ghi |
| Axit/lava/VFX/audio bổ sung | Chưa chọn | Chưa ghi | Chưa ghi |

- Điều khiển/camera (T2): Ball m_MaxSpeed 9, m_Acceleration 25, m_Brake 8 (Player.prefab); joystick dead zone 0,1, kích thước 132/600 đơn vị canvas (≈ 22% chiều cao); vùng vuốt nửa phải, hết chiều rộng = 180° × độ nhạy (0,5–2, mặc định 1, lưu PlayerPrefs `camera_sensitivity`, thanh trượt trong menu tạm dừng); camera ThirdPersonOrbitCamera distance 7,8 (3–20), pitch 15–65°, va chạm SphereCast bán kính 0,25. Tạm dừng: ESC, nút “II” trên mobile, mất focus.
- Giá trị cuối và vị trí config chỉnh được (N, spawn cap/interval, enemy speed/budget, camera, boost nếu có): Config tại `Assets/Resources/Levels/Level1..4.asset` (ghi bằng Tools > Robo Lac Loi > Write Level Configs / `LevelConfigBuilder.WriteDefaults`): N 6/10/14/18; có sẵn 3/4/5/6; trần energy 5/6/7/8; interval 5–7/5–7/4–6/4–6 giây; enemy cap Easy/Normal/Hard L1 2/3/4, L2 4/5/6, L3 6/7/8, L4 8/9/10; boss cap 1/1/2/2. Giá trị khởi điểm, chưa cân bằng.
- Diện tích L1–2 trước/sau và ảnh top-down: Trước: L1 1021 m², L2 2237 m²; ảnh top-down tạo bằng `BaselineProbe.Run` (ghi ra `~/Downloads/claude/work/probe/Level{1,2}_topdown_before.png` trên máy build, không commit). Sau: Chưa đo.
- Report test tự động và ma trận chơi thử mục 12: Chưa chạy.
- Video Android multitouch, AI vượt vật cản và Ending: Chưa có.
- Kết quả frame time/memory/10 vòng retry trên thiết bị đã chọn: Chưa đo.
- Kết quả review Gate B/C/D/E và vấn đề cần xử lý: Chưa review.
- Hạng mục hoãn, lỗi đã biết và giới hạn bản giao: Chưa ghi.
