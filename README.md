# Telegram Deploy Bot by mnduc9802

## Giới thiệu

Bot Telegram này được thiết kế để hỗ trợ triển khai các dự án từ Jenkins. Bot cung cấp một số lệnh để người dùng có thể tương tác và thực hiện các hoạt động như triển khai dự án, xem trạng thái, gửi phản hồi, và nhiều hơn nữa.

## Concept

Bot được xây dựng dựa trên các khái niệm cơ bản sau:

1. **Command**: Các lệnh mà người dùng có thể gửi cho bot để thực hiện các chức năng cụ thể.
2. **Update**: Các cập nhật từ Telegram, bao gồm tin nhắn, callback query, và các sự kiện khác.
3. **Handler**: Các phương thức xử lý các cập nhật và thực hiện các hành động tương ứng.
4. **Jenkins Integration**: Kết nối và tương tác với Jenkins để triển khai các dự án.

## Logic

Bot được thiết kế để xử lý các lệnh và phản hồi từ người dùng thông qua các tin nhắn văn bản và các nút inline. Bot theo dõi trạng thái phản hồi của người dùng và cập nhật thông tin tương ứng.

## Trigger

1. Bot bắt đầu nhận cập nhật từ Telegram khi phương thức `StartReceiving` được gọi.
2. Mỗi cập nhật từ Telegram được xử lý bởi `HandleUpdateAsync`.

## Event

1. **Message**: Khi bot nhận được một tin nhắn văn bản từ người dùng, nó sẽ gọi `HandleMessageAsync` để xử lý tin nhắn đó.
2. **Callback Query**: Khi bot nhận được một callback query (từ một nút bấm inline), nó sẽ gọi `HandleCallbackQueryAsync` để xử lý.

## Command

### StartCommand
- **Mô tả**: Gửi tin nhắn chào mừng và các tùy chọn lệnh.
- **Lệnh**: `/start`
- **Hành động**: Gửi tin nhắn chào mừng.

### ProjectsCommand
- **Mô tả**: Hiển thị danh sách các dự án.
- **Lệnh**: `/projects`
- **Hành động**: Gửi tin nhắn với danh sách các dự án hiện tại.

### DeployCommand
- **Mô tả**: Triển khai một dự án được chọn.
- **Lệnh**: `/deploy`
- **Hành động**: Gửi tin nhắn xác nhận và thực hiện triển khai dự án.

### StatusCommand
- **Mô tả**: Hiển thị trạng thái của bot.
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
5. Sử dụng các lệnh như `/start`, `/projects`, `/deploy`, `/status`, `/help`, `/menu` hoặc `/feedback` để tương tác với bot.

---

## Ghi chú

- Đảm bảo rằng bot của bạn đã được kích hoạt và có quyền truy cập vào các API cần thiết từ Telegram.
- Đảm bảo rằng bạn đã cấu hình đúng thông tin kết nối tới Jenkins.
