# Kế hoạch cải tiến game 3D

## Mục tiêu

- Vuốt camera mượt, không bị vùng joystick kích hoạt nhầm.
- Phân bố năng lượng và bẫy có chủ đích nhưng vẫn ngẫu nhiên, luôn còn đường thắng.
- Tăng độ nguy hiểm của quái theo level; boss cuối chạm là chết.
- Bổ sung hướng dẫn trực quan cho quái, vật phẩm tốt và vật phẩm xấu.
- Thay asset khó hiểu bằng biểu tượng có nghĩa rõ ràng.
- Cho creep/boss chủ động tìm player bằng tìm đường, tốc độ tăng theo độ khó.

## Phạm vi mã cần xác định khi bắt đầu triển khai

Tìm các script/prefab tương ứng với `joystick`/`touch`/`camera`, `energy`/`pickup`/`trap`, `enemy`/`boss`/`damage`, `tutorial`/`guide`, scene và navmesh. Không sửa trực tiếp từng scene nếu có thể đưa thông số vào một `GameBalanceConfig` (ScriptableObject hoặc cấu hình tương đương).

## Thiết kế chi tiết

### 1. Input camera và vùng joystick

1. Tách pointer dùng cho di chuyển và pointer dùng cho camera; joystick giữ `pointerId` của nó từ lúc bắt đầu kéo đến lúc kết thúc.
2. Camera chỉ nhận `PointerDown/Move` nếu điểm bắt đầu nằm ngoài `joystickRect` mở rộng (hitbox joystick + khoảng đệm 24–48 px). Khi joystick đang kéo, mọi sự kiện cùng `pointerId` bị bỏ qua.
3. Vùng camera mặc định là 55–65% phía phải màn hình và toàn bộ chiều cao; cho phép cấu hình thành toàn màn hình trừ vùng joystick để dùng trên thiết bị khác tỉ lệ.
4. Tính delta từ vị trí pointer trước đó, không lấy delta từ tọa độ tuyệt đối; dùng `unscaledDeltaTime`, sensitivity theo DPI và smoothing có giới hạn. Reset trạng thái khi pointer mất/focus mất để tránh cú giật sau khi app nền.
5. Kiểm tra UI raycast trước khi nhận vuốt để nút pause/SFX/BGM không xoay camera.

**Tiêu chí nghiệm thu:** kéo joystick 30 lần không đổi hướng camera; vuốt ở vùng phải cho cùng góc xoay trên các tốc độ kéo khác nhau; pointer rời màn hình/nhấn pause không tạo snap; mouse trong Editor vẫn hoạt động.

### 2. Sinh năng lượng, bẫy và bảo đảm khả năng thắng

1. Mỗi level khai báo `requiredEnergy = x`, sau đó chọn tổng số pickup dương trong khoảng `[x+2, x+5]` bằng seed của lượt chơi.
2. Sinh mẫu vị trí trên toàn vùng walkable/navmesh bằng chia ô hoặc Poisson-disc; giới hạn khoảng cách tối thiểu giữa các pickup để không tạo cụm. Bắt buộc phủ các vùng/sector của map và dành một số điểm rủi ro (mép vực, gần boss/quái lớn) nhưng phải có khoảng tiếp cận hợp lệ.
3. Sinh trap quanh một phần pickup theo bán kính cấu hình, trộn các loại trừ năng lượng, trừ máu và làm chậm; có khoảng cách tối thiểu để không chồng collider và không tạo một vùng chết không thể né.
4. Tính trước `worstCaseNetEnergy`: tổng pickup dương trừ toàn bộ penalty bắt buộc có thể gặp trên tuyến hợp lệ. Nếu nhỏ hơn `x`, đổi vị trí/bớt penalty hoặc thêm pickup trong giới hạn; chỉ cho phép vượt `x+5` khi cần để sửa seed lỗi và ghi log cảnh báo.
5. Dùng seed hiển thị trong debug để tái hiện lỗi; lưu danh sách spawn sau khi bake để QA kiểm tra phân bố bằng heatmap.

**Tiêu chí nghiệm thu:** 1000 seed/level không có cụm pickup vượt ngưỡng; mọi level có ít nhất một tuyến đạt `requiredEnergy` sau penalty; pickup/trap trải trên các sector; không spawn trong collider, dưới địa hình hoặc ngoài navmesh.

### 3. Damage, tốc độ và boss cuối

1. Đưa máu, damage, cooldown, tốc độ di chuyển, tốc độ truy đuổi vào bảng theo level và difficulty; áp dụng qua một damage service thống nhất để player/enemy/trap dùng cùng quy tắc.
2. Quái tăng damage và tốc độ theo level với trần rõ ràng; hiển thị telegraph cho đòn mạnh để tránh cảm giác chết ngẫu nhiên.
3. Boss level cuối có `contactKill = true`: va chạm hợp lệ với hurtbox player gọi chết ngay, có i-frame/lock để không gọi nhiều lần. Tách hitbox boss khỏi collider điều hướng và kiểm tra layer để không chết khi chạm trigger môi trường.
4. Trước khi vào level cuối, hiển thị panel giới thiệu boss gồm ảnh, tên, quy tắc “chạm là chết”, mục tiêu và nút tiếp tục; chỉ bắt đầu AI sau khi panel đóng.

**Tiêu chí nghiệm thu:** cùng difficulty cho damage tăng đúng bảng; boss giết player ở lần chạm đầu tiên; panel chỉ hiện một lần theo tiến trình; restart level reset đúng trạng thái boss.

### 4. Hướng dẫn chơi

1. Tạo một mục Codex/Guide với các thẻ: điều khiển, creep, boss, năng lượng, hồi HP/tăng tốc, trừ HP, trừ năng lượng, làm chậm.
2. Mỗi thẻ có ảnh prefab/icon thật, tên tiếng Việt ngắn, tác dụng, màu phân loại và cảnh báo. Dùng cùng prefab/icon trong HUD và trong guide để tránh lệch nghĩa.
3. Thêm trang riêng cho level cuối với ảnh boss và quy tắc chạm là chết; cho phép mở lại từ menu pause.
4. Bản địa hóa chuỗi và mô tả qua một bảng dữ liệu thay vì hard-code trong UI.

### 5. Asset và quy ước nhận diện

Đổi asset “cổng” hiện tại thành các icon/mesh dễ đọc, giữ màu và hình nhất quán:

| Tác dụng | Hình đề xuất | Màu | Feedback |
|---|---|---|---|
| Tăng HP | dấu cộng rõ nét | xanh lá | âm thanh hồi phục, số `+HP` |
| Trừ HP | dấu trừ/giọt vỡ | đỏ/cam | flash đỏ, số `-HP` |
| Tăng tốc | tia sét sáng | vàng/xanh | trail ngắn |
| Giảm tốc | tia sét đảo màu hoặc bị gạch | tím/xanh đậm | hiệu ứng chân nặng |
| Năng lượng | thanh/crystal cyan | cyan | cập nhật HUD |
| Trừ năng lượng | crystal nứt hoặc dấu `-` | đỏ tím | rung nhẹ HUD |

Mỗi asset cần collider, pivot, scale, layer và material emissive kiểm tra trên nền sáng/tối. Không dùng màu làm tín hiệu duy nhất; thêm hình dạng, nhãn khi nhặt và hiệu ứng âm thanh.

### 6. AI tìm đường và tốc độ

1. Bake/kiểm tra NavMesh toàn map, đánh dấu vùng vực/độc/địa hình không đi được đúng layer; thêm `NavMeshObstacle` cho vật cản động.
2. Creep/boss có state `ChasePlayer` mặc định, lấy player từ game session ngay khi spawn; không chờ khoảng trigger. Dùng repath theo khoảng 0.15–0.35 giây, chỉ cập nhật khi player đổi ô đủ lớn để tránh tốn CPU.
3. Khi không có đường thẳng, dùng `NavMeshAgent` tới vị trí player; nếu path invalid thì chuyển về điểm thấy cuối cùng và thử lại với fallback radius. Boss có ưu tiên đường ngắn và không bị kẹt ở mép vực.
4. Difficulty map vào tốc độ agent, acceleration, stopping distance và repath; Easy/Normal/Hard tăng dần, boss dùng profile riêng. Giới hạn tốc độ trên dốc và khi knockback.
5. Thêm debug gizmo đường đi, trạng thái agent, thời gian repath và lý do mất mục tiêu.

**Tiêu chí nghiệm thu:** sau khi spawn, AI bắt đầu hướng về player dù player đứng xa; 30 lượt chạy không kẹt ở vật cản; tốc độ tăng đúng Easy < Normal < Hard; boss không đi xuyên vực và vẫn tìm được đường vòng.

## Thứ tự triển khai

1. **Chuẩn hóa dữ liệu và đo hiện trạng:** lập bảng level/difficulty, chụp seed và ghi lại vị trí spawn, đo frame time input/AI.
2. **Input camera:** sửa pointer ownership và vùng loại trừ; test trên Editor và thiết bị cảm ứng.
3. **Spawner năng lượng/bẫy:** làm solver bảo đảm khả năng thắng, sau đó tune phân bố bằng heatmap.
4. **AI/NavMesh:** bake lại navmesh, bật chase mặc định, tune tốc độ theo difficulty.
5. **Damage/boss và panel giới thiệu:** áp dụng bảng cân bằng và kiểm tra reset/restart.
6. **Guide và asset:** thay prefab/icon, thêm mô tả và liên kết dữ liệu.
7. **QA hồi quy và đóng gói:** chạy test seed, test cảm ứng, test level/boss, profiling mobile; chỉ phát hành sau khi mọi tiêu chí nghiệm thu đạt.

## Bộ kiểm thử tối thiểu

- 3 thiết bị/tỉ lệ màn hình, mouse và touch; joystick kéo liên tục trong khi vuốt ở các vùng khác nhau.
- 1000 seed cho mỗi level; báo cáo min/max pickup, penalty, khoảng cách gần nhất và khả năng thắng.
- Easy/Normal/Hard với cùng seed; so sánh tốc độ, damage, repath và thời gian tới player.
- Va chạm boss, restart, pause/resume, mất focus, chết và load lại scene.
- Kiểm tra đọc hiểu asset bằng grayscale/độ sáng thấp và người chơi mới mở guide không cần vào gameplay.

## Rủi ro và điểm cần chốt trước khi code

- Nếu dự án đang dùng hệ input cũ và Input System mới cùng lúc, phải chọn một luồng pointer để tránh nhận sự kiện hai lần.
- Solver spawn cần biết navmesh/tuyến hợp lệ; nếu level chưa có navmesh đáng tin cậy, phải bake lại trước khi tune AI.
- Cần chốt danh sách loại penalty, damage và ngưỡng tốc độ cho từng level; nên lưu trong config để cân bằng mà không sửa code.
