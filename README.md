# Thereasm

Game thẻ bài chiến thuật và suy luận ẩn danh dành cho 2 người chơi phát triển trên Unity.

![Thereasm Gameplay](Docs/Media/gameplay.webp)

## Giới thiệu
- **Thể loại:** Strategy / Deduction Card Game
- **Chế độ chơi:** Đấu với Bot (PvE) hoặc Chơi online qua mạng (PvP via Photon Fusion).
- **Hỗ trợ ngôn ngữ:** Tiếng Việt & Tiếng Anh.

### Lối chơi cơ bản
- **Phe Dân Làng (Village):** Lần theo dấu vết, quan sát nước đi và kết quả bỏ phiếu để tìm ra kẻ nguyền rủa trước khi quá muộn.
- **Phe Cursermancer:** Gieo rắc lời nguyền, đánh lạc hướng và dẫn dụ Dân làng bỏ phiếu sai.
- Quản lý điểm năng lượng (Hearts) để ra bài, di chuyển và dùng kỹ năng bài trung lập.

## Yêu cầu môi trường & Cài đặt
1. **Unity Version:** Khuyến nghị **Unity 6000.0.29f1** (Unity 6).
2. Clone repo về máy và mở bằng Unity Hub.
3. Cài đặt **Photon Fusion 2.0.13** (tải package từ trang chủ Photon Engine và import vào project).
4. Mở scene menu chính tại: `Assets/Scenes/MenuScenes.unity` và nhấn **Play**.

## Thao tác phím chuột
- **Chuột trái:** Chọn bài, đặt bài, kéo thả để vote.
- **Chuột phải:** Xem chi tiết lá bài / Kéo để chọn mục tiêu dùng kỹ năng.
- **Con lăn chuột:** Phóng to / thu nhỏ bàn cờ.
- **Esc:** Bật menu / tạm dừng.

## Tài liệu chi tiết
- [Luật kỹ năng các thẻ bài](Docs/CARD_SKILL_RULES.md)
- [Cấu hình Photon Online](Docs/PHOTON_ONLINE.md)
