# Luật tương tác kỹ năng

Tài liệu quy định chi tiết về cách các kỹ năng thẻ bài tương tác và giải quyết trong Thereasm. Dữ liệu thẻ và thông số kỹ năng có thể tinh chỉnh trực tiếp trong Inspector tại Assets/ScriptableObjects và Assets/Skill_Neutral.

- **Trader:** giải quyết cộng/trừ phiếu của các kỹ năng khác trước, rồi Trader mới chia. Lấy **số phiếu cao nhất**, không lấy tổng, trong nhóm bản thân và các lá kề ngang. Chỉ những lá thực sự đang ngồi mới tham gia chia; làm tròn xuống và bỏ phần dư. Hàng xóm được xác định bằng vị trí ghế, kể cả ghế phụ, không bằng thứ tự lưu trong danh sách.
- **Prankster:** có lá trong ghế kề bên trái là có thể kích hoạt, không chỉ khi đặt Prankster. Cú đẩy thường đưa mục tiêu sang một ghế chính đang trống. Cơ hội 10% đẩy về tay chỉ có khi vừa đặt Prankster. Elder chặn hiệu ứng này. Các phản ứng nối tiếp được xử lý sau khi hoàn tất một thao tác đổi chỗ, tránh nhìn thấy trạng thái hai lá mới đổi được nửa chừng.
- **Saboteur:** chỉ khóa kỹ năng gốc của lá bài, kể cả hào quang và nội tại giảm giá của Totem. Không khóa khả năng lây nhiễm của Cursermancer. Detective bị câm lặng không được cộng điểm nghi ngờ.
- **Game Master:** có bài trên ghế phụ thì bị khóa di chuyển/đổi chỗ bằng thao tác người chơi hoặc bot. Hiệu ứng của lá khác vẫn có thể đẩy nó: nếu sang ghế khác, ghế phụ và bài trên đó đi theo; nếu về tay, các bài trên ghế phụ cùng về tay chủ gốc. Nếu Game Master chết, các bài trên ghế phụ cùng chết, kể cả có khiên Preacher. Nếu ghế phụ bên trái mất khi rời ghế số 1, bài trên riêng ghế đó trở về tay.
- **Hunter và Witch:** giữ hồi kỹ năng sau khi kết thúc pha hiến tế/xử tử. Witch không quét chính nó hoặc bài trên tay. Hunter bắn vào khiên Preacher vẫn tốn tim và lượt bắn, nhưng không giết mục tiêu.
- **Phiếu bầu:** lá chết mất phiếu đã bỏ; các phiếu đang nhắm vào lá chết cũng bị hủy. Khi ghế phụ bị xóa, phiếu ở các ghế còn lại được đổi chỉ số tương ứng, tránh chuyển nhầm mục tiêu.
- **Gambler:** mục tiêu không hợp lệ không tốn tim và không đăng ký tham gia cược. Khi thua cược, trừ phiếu ở mục tiêu mà phe đó bỏ nhiều phiếu nhất. Nếu hòa, giữ cách ưu tiên mục tiêu được bỏ phiếu trước. Dấu cược và trạng thái đã cược chỉ gửi cho đúng người cược khi chơi online; cược của hai người không ghi đè nhau.
- **Snooper:** khi nhiễm bệnh, chỉ phe Cursermancer được nhắm phiếu, kể cả trong chế độ máy. Không dùng chủ sở hữu gốc của lá Cursermancer bí mật để suy ra quyền điều khiển. Mục tiêu được nhớ bằng lá bài, không bằng chỉ số ghế có thể thay đổi. Mục tiêu Decay và trạng thái đã dùng được giữ riêng cho Cursermancer trước tổng kết. Khi tổng kết, công khai mục tiêu đã nhận phiếu của Snooper cho cả hai phe, kể cả có thể nhận ra Cursermancer.
- **Invoker:** chỉ nhận lựa chọn còn hợp lệ khi Invoker còn sống, còn trên ghế sát nghĩa trang, không bị câm lặng, đúng người điều khiển và đúng pha. Hồi sinh tiêu thụ đúng một xác, kể cả xác đang bay hoặc đang nằm trong bảng nghĩa trang mở. Lá hồi sinh giữ danh tính Cursermancer nếu có và được thanh tẩy nhiễm bệnh.
- **Blind:** phải ngồi sát nghĩa trang. Đọc lá chết mới nhất từ dữ liệu nghĩa trang, kể cả khi hiệu ứng bay chưa kết thúc; mở/đóng bảng nghĩa trang không thay đổi lá được đọc. Dân làng thấy kết quả công bố, bao gồm kết quả giả khi Blind bị nhiễm; Cursermancer luôn thấy sự thật, không cần giữ nút. Nút xem bí mật đã bỏ.
- **Detective và Reeve:** Detective được dùng lại sau khi thu về và đặt lại theo tag Once. Reeve phải còn hợp lệ và còn dân làng đáp ứng điều kiện khi hiệu ứng chuyển pha trễ thực sự chạy.

## Kiểm thử

Bộ kiểm thử kiểm tra tương tác kỹ năng nằm tại `Tests/Unity/SkillInteractionValidation.cs`.

Chạy kiểm thử thông qua script:
```powershell
Tools/Run-TutorialValidation.ps1 -Suite Skills
```
