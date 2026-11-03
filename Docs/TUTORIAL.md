# Tutorial tương tác

Mở MenuScenes và chọn **Học cách chơi · hướng dẫn từng bước**, hoặc mở trực tiếp **Assets/Scenes/TutorialBoard.unity** rồi Play. Màn này chạy offline, không cần Photon SDK. Có 40 bước ngắn; các bài thực hành tự chuyển khi thao tác thành công.

Bài học dùng 7 lá cố định: 1, 2, 3 thuộc nhóm phe Cursermancer; 9, 10, 11, 12 thuộc nhóm Dân làng. Người học lần lượt thao tác cả hai phe: chọn hai Cursermancer, xem/đóng mô tả, kéo đặt bài, dùng tim, chuyển lượt, nhận biết góc nhìn riêng của từng phe, bắn bằng Hunter, xem nghĩa trang, nguyền rủa, bỏ từng phiếu, chốt phiếu, xem tổng kết và thanh trừng. Bước nghi thức nói rõ đây là tình huống được đặt sẵn; phần chọn trung lập được bỏ qua trong bài học.

Viền vàng đánh dấu nguồn, viền xanh đánh dấu đích; mũi tên chỉ hướng kéo. Có hướng dẫn nút chuột, giải thích tác dụng, thông báo thành công và tiến độ. Khi kéo nhầm ghế hoặc chọn nhầm mục tiêu, thao tác bị từ chối để người học thử lại. Các nút chưa đến bước học bị khóa. **Học lại**, **Về menu** và **Xem luật đầy đủ** luôn có sẵn; khi hoàn thành có **Chơi thử với bot**.

Cột hướng dẫn tách **ĐẾN LƯỢT BẠN** và **LỜI THÌ THẦM**. Lời kể dùng giọng người đồng hành: gợi tò mò, nhắc giữ bí mật, động viên khi làm đúng. Mỗi bước vẫn nêu mục tiêu chiến thuật và điều cần quan sát sau thao tác: đặt bài để có phiếu, đưa Hunter lên bàn trước khi bắn, phân biệt nhiễm bệnh với vai trò Cursermancer, và chọn thời điểm Thanh trừng. Lời dẫn mở đầu bằng đêm đầu tiên trong làng rồi đi qua một vòng chơi, thay vì chỉ liệt kê lệnh bấm.

Lớp sáng vàng phủ toàn bộ hình lá bài nguồn, theo đúng RectTransform của hình bài, kèm viền mảnh bên ngoài. Mục tiêu kéo thả được phủ xanh; biển số 1/2 chỉ rõ nút chuột. TutorialSpotlight dùng các Image lưu sẵn để làm tối phần bàn gây xao nhãng, chừa cả nguồn và đích sáng. Biển chỉ dẫn được giữ trong vùng bàn, không đè cột nội dung. Các lớp này không nhận raycast. Khi tổng kết phiếu, lớp tối tắt để người học xem toàn bộ kết quả.

Tay bài trong tutorial xếp thành hàng thẳng, có khoảng cách giữa các lá để nhìn và bấm đầy đủ từng lá. Có thể chỉnh **DeckManager > Separate Hand Cards / Separated Card Gap** trong TutorialBoard. Lá bài không nhô lên khi rê chuột trong bài học. Các bước chỉ yêu cầu bấm chuột không được khởi tạo kéo, tránh làm mất lần bấm khi chuột lệch nhẹ; bước kỹ năng Thợ săn vẫn nhận kéo chuột phải.

## Chỉnh trong Unity Editor

- Nội dung: **Assets/UI/Tutorial/FirstMatchLesson.asset**, sửa Title, Instruction, Purpose, Success và Inspect Description trong Inspector. Giữ nguyên thứ tự Stage và bộ 7 lá nếu chưa sửa logic bài học.
- Bố cục: **TutorialBoard > Thereasm UI > Safe Frame — 1600 x 900 > Controls — buttons and setup > Tutorial Guide — editable instructions**. Các Text, nút và thanh tiến độ là object lưu sẵn trong scene.
- Khung chỉ dẫn: **Tutorial Focus — follows real targets**. Ảnh viền là Sprite tham chiếu trực tiếp; không chặn raycast, không che tâm lá bài.
- Độ tối và vùng tập trung: **TutorialSpotlight** trên cùng object, với Play Area và 15 Shade Image. Chỉnh màu/alpha từng Image trong Inspector; nguồn và đích được cập nhật theo vị trí thật.
- Controller giữ tham chiếu trực tiếp tới TurnManager, DeckManager, nút thật, khay bài và lesson asset. Các bài thực hành dùng luật và hàm thao tác thật của game.

Không chạy lại Tools/author_tutorial.py sau khi chỉnh scene bằng tay; công cụ này chỉ dùng một lần và từ chối ghi đè scene đã tồn tại.

## Sổ tay bảy ghế

MainBoard, TutorialBoard và NotePanel.prefab có đủ bảy Note Seat, đánh số 1–7; kho giữ các thẻ số 1–16. Nhãn số ghế không tham gia LayoutGroup để luôn thẳng với ô tương ứng. NotePanel có Canvas riêng ở lớp 500, trên thoại lá bài ở lớp 200; DialogueModalGuard ẩn thoại ngay khi mở và ngăn thoại xuất hiện đến khi đóng. Thẻ số được kéo trong Canvas của sổ để không biến mất phía sau cửa sổ. Bộ kiểm tra `Tools/Run-TutorialValidation.ps1 -Suite Support` thử thả thẻ 7 vào ghế 7, thay bằng thẻ 8, đưa thẻ cũ về kho và kiểm tra chữ hai ngôn ngữ.

## Kiểm thử

- Kiểm tra cú pháp và layout: Tools/check_ui_layout.py
- Kiểm thử tự động bài học tutorial:
`powershell
Tools/Run-TutorialValidation.ps1
`

## Bài thực hành ngân sách tim

Sau Thanh trừng có 13 bước riêng về tim: giải thích tim là sức hành động, đặt lá số 12 (5→3), chuyển ghế (3→2), tự rút (2→4), qua một vòng lượt thật để hồi 5, đặt bài nhóm đối phương (5→0), hồi theo lượt, chuyển bài đối phương (5→2), rồi thay từ tay xuống ghế có bài (5→0). Khóa chờ được mô phỏng là đã hết và có ghi rõ trong lời dẫn. Không thay đổi luật hoặc giá thao tác của ván chính.

Ba bảng cuối giải thích toàn bộ giá đổi chỗ trên bàn và từ tay, ép rút, giới hạn hoàn tim, Totem, Prankster, Hunter/Witch, kỹ năng miễn phí và Thanh trừng giới hạn lượt dùng. Nội dung chỉnh trong FirstMatchLesson.asset; panel Heart Workshop Reference và các tham chiếu nằm sẵn trong TutorialBoard, xem và sửa được trong Editor.

Nhãn thao tác dùng hai làn trên/dưới lá bài, tự giới hạn trong vùng chơi và tách nhau nếu còn giao nhau. Thoại vẫn được giữ; chỉ tạm ẩn khi khung thoại thực sự chạm nhãn chỉ dẫn. Hiệu ứng FloatingUI và material chữ giữ nguyên.

Xem bài → Esc → Tiếp tục: FastUnzoom trả lại quyền chuột cho CanvasDimmer. Fade dùng thời gian không bị tạm dừng và nhả raycast ngay khi đóng, tránh lớp tối vô hình chặn cả bàn. Kiểm tra runtime thực hiện lại thao tác xem bài sau khi tiếp tục.

Mỗi mức giá được viết thành câu có đơn vị rõ ràng, ví dụ “Đưa bài thuộc phe bạn từ tay xuống ghế trống tốn 2 tim”. Phần phí thường và phí đổi chỗ được tách thành hai trang để dễ đọc; không dùng ghi tắt kiểu “bài mình 2”.

## Giọng kể

Tools/Localization/voice_tutorial.py giữ 40 bước kể bằng hai ngôn ngữ và bốn trang giải thích tim. Mỗi bước gồm một nhan đề gợi hình, thao tác cụ thể, lời dẫn giải thích ý nghĩa và lời phản hồi xác nhận kết quả. Giọng kể là người đồng hành trong ngôi làng, tránh diễn đạt như tài liệu lệnh. Tên thẻ giữ tiếng Anh; mọi mức giá ghi rõ số tim và chiều tiêu/hoàn. Nội dung được ghi vào FirstMatchLesson và catalog để sửa được trong Inspector. tutorial_voice.json là bản dữ liệu dễ đọc để đối chiếu.
