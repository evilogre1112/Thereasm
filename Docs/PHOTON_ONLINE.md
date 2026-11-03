# Chơi online hai người bằng Photon Fusion

Đã lưu App ID `REMOVED` trong **Assets/Networking/PhotonOnlineConfig.asset**. Mở asset trong Inspector để đổi App ID, App Version, Region, tên scene bàn chơi và chu kỳ đồng bộ. Mặc định hai máy dùng `asia`, `thereasm-online-4` và chu kỳ kiểm tra thay đổi 0,05 giây. Cần build lại cả hai phía bằng phiên bản này; bản cũ không dùng chung phòng với bản mới.

## Bước cần làm trong Unity

Dự án đã có **Photon Fusion 2.0.13 Stable 2379**, import từ gói chính thức bạn tải về. SDK nằm trong **Assets/Photon**, giữ nguyên GUID và metadata của nhà phát hành.

1. Thoát Play Mode và chờ Unity import/biên dịch các asset mới. Không cần tải hay import SDK lần nữa.
2. `THEREASM_FUSION` và **Allow unsafe code** đã bật cho bản Windows. `PhotonFusionSetup` cũng tự nhận SDK khi đổi build target, giữ các define khác của dự án. Có thể chạy lại **Tools > Thereasm > Check Photon Fusion Setup**.
3. **Assets/Photon/Fusion/Resources/PhotonAppSettings.asset** đã có App ID, App Version và region `asia`. **NetworkProjectConfig.fusion** giữ Peer Mode Single cho trò chơi bình thường. Hai người chơi được đặt qua StartGameArgs.PlayerCount = 2.
4. Sửa thông số trò chơi trong **Assets/Networking/PhotonOnlineConfig.asset** bằng Inspector. Chọn **Tools > Thereasm > Apply Saved Photon Settings** để đồng bộ sang asset SDK. Adapter cũng áp dụng bản sao cấu hình này mỗi lần kết nối.
5. Mở **Assets/Scenes/MenuScenes.unity**, chọn **Online**. Build Windows một bản rồi chạy cùng Unity Editor, hoặc chạy hai bản build trên hai máy. MenuScenes và MainBoard đã có trong Build Settings.

**Tools > Thereasm > Photon Fusion Setup** mở cửa sổ chọn config và áp dụng lại thiết lập. Nếu cần cài SDK trên một dự án mới, cửa sổ cũng có liên kết [SDK chính thức](https://doc.photonengine.com/fusion/v2/getting-started/sdk-download) và nút import .unitypackage.

Manifest đã đăng ký trực tiếp **com.unity.nuget.mono-cecil 1.11.4**, cùng bản đã có trong cache của Unity, để trình cài Fusion nhận dependency và bật các define/weaver chính thức. Allow unsafe code được bật trước khi vào Play Mode để tránh biên dịch lại giữa lúc kết nối.

## Tạo phòng, chọn phe và đổi phe

- Bấm **Chơi online · 2 người**. Người thứ nhất bấm **Tạo phòng**; để trống mã sẽ tạo mã ngẫu nhiên 6 ký tự.
- Người thứ hai nhập cùng mã rồi bấm **Vào phòng**. Mã được chuẩn hóa viết hoa, dài 4–20 ký tự, gồm A–Z, 0–9 và dấu `-`.
- Khi vào phòng, khách tự nhận phe đối diện chủ phòng. Cả hai đều có thể bấm **Chọn phe Cursermancer / Chọn phe Dân làng** trước trận. Chọn phe đối phương đang giữ sẽ đổi vai hai người, luôn giữ một người mỗi phe. Khách cũng có quyền yêu cầu đổi phe; chủ phòng xác nhận trạng thái chung và khách gửi xác nhận đã nhận.
- Khi đủ 2 người và phe đã đồng bộ, **chủ phòng** bấm **Bắt đầu ván**. **Người chơi 1 (Cursermancer) chọn 4 lá trung lập đầu, người chơi 2 (Dân làng) chọn 4 lá sau**, bất kể ai tạo phòng. Cả hai nhìn thấy cùng các lựa chọn và tiến độ; chỉ người tới lượt được chọn. Sau đó người giữ phe Cursermancer chọn Cursermancer bí mật trong hai nhóm bài gốc rồi bấm Bắt đầu ván.
- Kết thúc ván, bấm **Đổi phe ván sau** nếu muốn hoán đổi. Sau đó chủ phòng bấm **Chơi tiếp** để cả hai vào ván mới. Không đổi phe giữa ván đang diễn ra.
- Nếu một người mất kết nối/rời phòng, lượt chơi bị khóa và hiện thông báo. Về Menu rồi tạo phòng mới. Phiên này chưa hỗ trợ nối lại hoặc chuyển chủ phòng.

## Chỉnh giao diện trực tiếp trong Editor

Trong **MenuScenes > Thereasm UI**, Inspector có nút **Online** để hiện phòng chờ ở Edit Mode. Sửa **Safe Frame — 1600 x 900 > Online Lobby > Online Room Panel — edit layout here**: vị trí RectTransform, Image, Text, ô nhập mã và sự kiện OnClick đều được lưu sẵn trong scene.

Chọn root **Photon Session — configuration** để sửa tham chiếu Settings. Trong MainBoard, `OnlineGameBridge` nằm cùng object với TurnManager/DeckManager. Nút đổi phe nằm trong **Result Panel — preview by enabling**. UI và ảnh không được tạo bằng đường dẫn lúc chạy; các asset và component được gán bằng tham chiếu Unity.

Không chạy lại `Tools/add_online_ui.py` hoặc `Tools/migrate_scene_ui.py` sau khi sửa scene bằng tay. Đây là các công cụ authoring một lần, có chặn ghi đè scene đã chuyển đổi.

## Cách đồng bộ và giới hạn

Chủ phòng thực thi luật và giữ trạng thái thật. Máy khách gửi ý định chọn bài trung lập, đặt/trả/đổi bài, kỹ năng, nguyền rủa, bỏ phiếu, thanh trừng và kết thúc lượt; chủ phòng kiểm tra mã ván, thứ tự lệnh, phe, giai đoạn và điều kiện của từng thao tác trước khi áp dụng. Đổi phe không đổi chủ phòng.

Mỗi ván có GUID do chủ phòng cấp. Fusion tải cùng scene cho hai peer; khách yêu cầu lại thông tin phòng và trạng thái bàn sau khi tải. Khi chơi lại, bridge mới không nhận mã ván cũ. Lệnh khách được đánh số, gửi liên tiếp qua hàng đợi tối đa 16 lệnh và gửi lại nếu chưa được xác nhận; chủ phòng xử lý theo thứ tự, không thực thi lặp lại. Xác nhận không khóa toàn bộ thao tác của khách. Ghế dùng ID ổn định và lệnh ghi cả lá mục tiêu, tránh áp dụng lên ghế/lá khác khi bàn vừa thay đổi. Các callback kéo/thả cũ bị chặn ở khách để không tự trừ tim hoặc sửa bàn trước khi chủ phòng xử lý.

Kéo chuột trái trong lượt hành động di chuyển trực tiếp hình lá bài theo chuột trên máy khách, giống chủ phòng. Khi thả, hình lá bài hiện ngay tại ghế và hình hai lá đổi chỗ ngay nếu có mục tiêu; không cần chờ phản hồi mạng mới bắt đầu di chuyển. Chỉ phần hiển thị được dự đoán: tim, khóa bài và kỹ năng vẫn do chủ phòng xử lý. Snapshot tới trong lúc kéo giữ nguyên vị trí dưới chuột; xác nhận lệnh giải phóng phần hiển thị dự đoán để khớp bàn thật. Lệnh bị từ chối sẽ trả cả hai lá về trạng thái thật. Thả ra ngoài trả về vị trí cũ; mất kết nối hoặc chuyển giai đoạn cũng hủy kéo. Mũi tên mục tiêu dành cho kéo chuột phải dùng kỹ năng và kéo bỏ phiếu/nguyền rủa.

Trạng thái gửi cho khách giữ Dân làng loại bỏ cờ Cursermancer, nhiễm bệnh hiện tại, nhiễm bệnh trước khi chết và cờ đã lây nhiễm; công khai khi kết thúc trận. Phiếu bầu của phe khác được giấu trước tổng kết. Máy khách không tự chạy DeckManager/TurnManager để tránh chia bài, trừ tim và xử lý kỹ năng hai lần. Danh sách bài, thứ tự bài, các lựa chọn trung lập, tiến độ chọn và điều kiện bắt đầu được đồng bộ từ chủ phòng. Trạng thái được gửi khi thay đổi và gửi lại toàn bộ định kỳ để máy tải scene chậm vẫn nhận đủ bàn chơi. Khi chờ đối phương, người chơi vẫn xem bài và đổi khay bài được.

Adapter dùng Host/Client của Fusion và [Reliable Data Streaming](https://doc.photonengine.com/fusion/v2/manual/data-transfer/data-streaming) để truyền trạng thái bàn bài. Bàn cờ không cần NetworkObject trên từng lá. Đây là thiết kế chủ phòng đáng tin cậy: chủ phòng có trạng thái bí mật trong bộ nhớ; chưa có máy chủ chuyên dụng chống gian lận.

## Kiểm thử chế độ Online

- **Kiểm tra cú pháp và logic:** Tools/Verify-Game.ps1
- **Kiểm tra tích hợp Fusion SDK:** Tools/Run-TutorialValidation.ps1 -Suite Fusion
- **Kiểm tra kết nối 2 người chơi:** Tools/Run-TutorialValidation.ps1 -Suite Online
- **Kiểm tra quyền nhìn thông tin:** Tools/Run-TutorialValidation.ps1 -Suite Visibility

## Quyền xem thông tin theo phe

Cursermancer tự động thấy danh tính và nhiễm bệnh thật trên bài sống, bài chết và lựa chọn hồi sinh, kể cả khi Blind công bố thông tin giả. Nút giữ xem bí mật đã ẩn trong scene và bị tắt trong UI; tutorial chuyển bước này thành giải thích góc nhìn theo phe.

Dân làng thấy bài thường và thông tin công khai. Khi Blind có hiệu lực, Dân làng thấy đúng kết quả Blind công bố, kể cả kết quả giả do Decay; dữ liệu xác chết thật không gửi tới khách giữ Dân làng. Cửa sổ nghĩa trang, lá đang bay vào mộ và lá đang phóng to dùng cùng quy tắc hiển thị.

Mục tiêu và cờ đã dùng của Snooper Decay chỉ dành cho Cursermancer trước tổng kết. Khi tổng kết thực sự áp dụng phiếu, mục tiêu Snooper được công khai cho cả hai phe, ở cả dạng khỏe và dạng Decay, theo luật đã xác nhận. Dấu mục tiêu được xóa khi hết vòng.

Gambler giữ riêng mục tiêu và trạng thái cược của từng người; việc đối phương cược không thay đổi dấu cược của mình. Trước tổng kết, phiếu chỉ hiển thị cho đúng người bỏ phiếu. Invoker gửi lựa chọn chỉ cho người điều khiển; host không mở bảng lựa chọn của khách trên màn hình mình. Thông báo thao tác cũng được lọc theo người điều khiển.

`Tools/Run-TutorialValidation.ps1 -Suite Visibility` kiểm tra chính các hàm Capture/Apply của bridge và sprite/material/biểu tượng trong Unity thật. Báo cáo **Tests/Reports/OnlineVisibilityValidationResult.json** hiện có 149 assertions qua các trường hợp host giữ từng phe, Decay, cược, Invoker và kết thúc trận. Bộ thử hai tiến trình còn kiểm tra gói Photon thật tới khách giữ Dân làng không chứa danh tính, nhiễm bệnh, cờ lây nhiễm hoặc trạng thái dùng Snooper bí mật.

Giới hạn kiến trúc: host vẫn là máy thực thi luật và giữ dữ liệu thật, kể cả khi người chủ phòng chơi Dân làng. Các sửa đổi bảo vệ giao diện host và gói trạng thái gửi cho khách; chúng không ngăn chủ phòng cố tình đọc bộ nhớ hoặc lệnh gửi tới tiến trình server. Muốn bí mật cả với một chủ phòng không đáng tin cần chuyển xử lý luật sang máy chủ riêng. Hai phía phải build lại với **thereasm-online-4** để không dùng chung phòng với bản lọc thông tin cũ.
