# Ngôn ngữ và nhận diện bài

Tiếng Anh là mặc định của dự án và bản English. Bản Vietnamese khởi động bằng tiếng Việt. Cả hai bản đều có nút English / Tiếng Việt; lựa chọn được nhớ trên máy cho các lần chơi sau. Hai người trong cùng phòng có thể dùng hai ngôn ngữ khác nhau.

## Chỉnh trực tiếp trong Unity

- Chọn `Assets/UI/Localization/GameLocalization.asset`. Inspector có ô tìm theo nội dung/key, hai cột English và Vietnamese, ngôn ngữ mặc định và các font được gán trực tiếp.
- Mở MenuScenes, MainBoard hoặc TutorialBoard, chọn `Thereasm UI`. LanguageController có Preview Language để xem từng ngôn ngữ ngay trong Editor. Các nút và nhãn là đối tượng được lưu trong scene, có thể chỉnh RectTransform như UI bình thường.
- `Assets/Prefabs/CardUI.prefab` có CardOwnershipView với các tham chiếu tới Owner Badge và Outline. Có thể chỉnh màu cho bài của mình, đối thủ và trung lập tại đây. Các phần này không chặn raycast của lá bài.
- Font UGUI là Liberation Sans đã có trong dự án. Font TMP `Assets/UI/Localization/Thereasm Bilingual SDF.asset` đã chứa dấu tiếng Việt, không cần font cài trên máy người chơi.
- LocalizedLabel giữ material TMP gốc (shader, outline, glow, underlay); chỉ đổi atlas cho font có dấu. FloatingUI và các thông số lượn sóng của mô tả/thoại vẫn giữ nguyên. Muốn thay kiểu chữ, chỉnh Original Text Material trên LocalizedLabel cùng material của TMP. Khung thoại dùng cùng asset khung cổ của game, chữ màu giấy và nằm giữa khung.
- `Assets/Prefabs/DraftCard.prefab` là prefab riêng của màn chọn bài trung lập. DescText cũng có LocalizedLabel để đổi ngôn ngữ ngay cả khi đang mở màn chọn; DeckManager gán mô tả đã dịch ngay khi tạo lá.

## Ý nghĩa nhãn

- Trên tay bài, tiêu đề chung ghi YOUR HAND / TAY BÀI CỦA BẠN hoặc OPPONENT'S HAND / TAY BÀI ĐỐI THỦ. Không lặp thanh nhãn màu trên mỗi lá đang xòe. Các tab chỉ ghi tên nhóm bài và tô đậm nhóm đang xem.
- Trên bàn/nghĩa trang và khi phóng to, nhãn viền cổ nhỏ nằm giữa hai số ở đầu lá: vàng giấy cho YOURS / CỦA BẠN; đồng trầm cho OPPONENT / ĐỐI THỦ; xám giấy cho NEUTRAL / TRUNG LẬP. Bài Neutral luôn chỉ ghi Trung lập, không gắn về phía người đặt, bạn hay đối thủ.
- Khung thoại bám VisualChild để luôn theo hình lá bài khi di chuyển. Khi xem mô tả, vị trí phóng to căn giữa cả lá bài và bảng mô tả, tránh dồn toàn bộ nội dung sang phải.
- Nhãn nhóm và viền không dựa vào vai trò bí mật, nhiễm bệnh, Decay hay lựa chọn kỹ năng riêng tư.
- Giữ nguyên tên tiếng Anh in sẵn trong ảnh bài, ở cả hai ngôn ngữ. Không có lớp nền hoặc nhãn dịch đè lên tên gốc. Chỉ dịch mô tả, thoại và giao diện.

## Kiểm thử

- Kiểm tra hai ngôn ngữ: Tools/Run-TutorialValidation.ps1 -Suite Localization
- Kiểm tra hiển thị menu: Tools/Run-TutorialValidation.ps1 -Suite MenuModes

Tên thẻ giữ nguyên tiếng Anh (Game Master, Preacher, Elder, Hunter, Detective...). Các nội dung mô tả, luật, phe, trạng thái và giao diện điều khiển hỗ trợ đầy đủ song ngữ English / Tiếng Việt.
