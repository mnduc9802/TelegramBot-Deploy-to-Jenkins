# Telegram Bot

## Concept

Bot Telegram này cung cấp các chức năng quản lý dự án, triển khai dự án, theo dõi trạng thái và phản hồi. Nó giúp người dùng tương tác với bot để thực hiện các thao tác liên quan đến dự án một cách thuận tiện và dễ dàng.

## Logic

Bot được thiết kế để xử lý các lệnh và phản hồi từ người dùng thông qua các tin nhắn văn bản và các nút inline. Bot theo dõi trạng thái phản hồi của người dùng và cập nhật thông tin tương ứng.

### Các Thành Phần Chính

1. **Commands**: Xử lý các lệnh từ người dùng.
2. **Callback Queries**: Xử lý các phản hồi từ các nút inline.
3. **Feedback State**: Theo dõi trạng thái phản hồi của người dùng để xử lý phản hồi một cách chính xác.
4. **Deployment Simulation**: Giả lập quá trình triển khai dự án và thông báo kết quả cho người dùng.

## Trigger

1. **Tin nhắn văn bản từ người dùng**: Khi người dùng gửi tin nhắn với các lệnh như `/start`, `/projects`, `/deploy`, `/status`, `/help`, hoặc `/feedback`.
2. **Callback query từ các nút inline**: Khi người dùng nhấn các nút inline như "Projects", "Deploy Project", "Status", "Help", hoặc các nút xác nhận triển khai.

## Event

1. **Lệnh `/start`**: Kích hoạt khi người dùng gửi lệnh `/start`.
2. **Lệnh `/projects`**: Kích hoạt khi người dùng gửi lệnh `/projects`.
3. **Lệnh `/deploy`**: Kích hoạt khi người dùng gửi lệnh `/deploy`.
4. **Lệnh `/status`**: Kích hoạt khi người dùng gửi lệnh `/status`.
5. **Lệnh `/help`**: Kích hoạt khi người dùng gửi lệnh `/help`.
6. **Lệnh `/feedback`**: Kích hoạt khi người dùng gửi lệnh `/feedback`.
7. **Callback query "Projects"**: Kích hoạt khi người dùng nhấn nút "Projects".
8. **Callback query "Deploy Project"**: Kích hoạt khi người dùng nhấn nút "Deploy Project".
9. **Callback query "Status"**: Kích hoạt khi người dùng nhấn nút "Status".
10. **Callback query "Help"**: Kích hoạt khi người dùng nhấn nút "Help".
11. **Callback query xác nhận triển khai**: Kích hoạt khi người dùng nhấn nút "Yes" hoặc "No" trong quá trình xác nhận triển khai.

## Command

### StartCommand
- **Mô tả**: Gửi tin nhắn chào mừng và các tùy chọn lệnh.
- **Lệnh**: `/start`
- **Hành động**: Gửi tin nhắn với các nút "Projects", "Deploy Project", "Status", "Help".

### ProjectsCommand
- **Mô tả**: Hiển thị danh sách các dự án.
- **Lệnh**: `/projects`
- **Hành động**: Gửi tin nhắn với danh sách các dự án hiện tại.

### DeployCommand
- **Mô tả**: Triển khai một dự án được chọn.
- **Lệnh**: N/A (kích hoạt từ callback query)
- **Hành động**: Gửi tin nhắn xác nhận và thực hiện triển khai dự án.

### StatusCommand
- **Mô tả**: Hiển thị trạng thái của bot và thông báo nếu mất kết nối mạng.
- **Lệnh**: `/status`
- **Hành động**: Gửi tin nhắn trạng thái bot và kiểm tra kết nối mạng.

### HelpCommand
- **Mô tả**: Hiển thị hướng dẫn sử dụng các lệnh của bot.
- **Lệnh**: `/help`
- **Hành động**: Gửi tin nhắn hướng dẫn.

### FeedbackCommand
- **Mô tả**: Yêu cầu và xử lý phản hồi từ người dùng.
- **Lệnh**: `/feedback`
- **Hành động**: Gửi tin nhắn yêu cầu phản hồi và xử lý phản hồi từ người dùng.

---

## Cách Chạy Bot

1. Cài đặt .NET 8.
2. Sao chép mã nguồn của bot.
3. Thêm token của bot vào mã nguồn.
4. Chạy chương trình bằng lệnh `dotnet run`.
5. Sử dụng các lệnh như `/start`, `/projects`, `/deploy`, `/status`, `/help`, hoặc `/feedback` để tương tác với bot.

---

**Lưu ý**: Đảm bảo bot của bạn đã được bật và có quyền truy cập Internet để nhận và gửi tin nhắn.
