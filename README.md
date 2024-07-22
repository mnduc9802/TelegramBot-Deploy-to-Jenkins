# TelegramBot

## Concept

Bot Telegram hoạt động như một công cụ để triển khai (Deploy) dự án với các chức năng chính như: Khởi tạo, Xóa tin nhắn, Hiển thị danh sách các lệnh, cung cấp tùy chọn để triển khai các dự á. Bot có thể nhận lệnh từ người dùng và phản hồi với thông tin phù hợp hoặc thực hiện các hành động cụ thể.

## Logic

### Khởi tạo Bot:

Bot được khởi tạo với token và bắt đầu nhận các cập nhật từ Telegram.

### Xử lý Cập nhật (Updates):

Khi nhận một tin nhắn hoặc callback query, bot kiểm tra loại cập nhật và dữ liệu liên quan.
Dựa vào loại cập nhật, bot thực hiện các hành động tương ứng (như xử lý tin nhắn hoặc callback query).

### Xử lý Lệnh:

Bot xử lý các lệnh /start, /clear, /help và callback queries để thực hiện các hành động cụ thể như hiển thị thông tin, xóa tin nhắn, hoặc xử lý yêu cầu triển khai dự án.

### Cập nhật Giao diện Người Dùng:

Bot gửi các tin nhắn và bàn phím inline (inline keyboards) để tương tác với người dùng, cung cấp lựa chọn và yêu cầu xác nhận.

## Trigger

### Tin nhắn đến:

Khi bot nhận được tin nhắn từ người dùng, bot sẽ kiểm tra nội dung tin nhắn để xác định lệnh cần thực hiện (/start, /clear, /help).

### Callback Query:

Khi người dùng nhấn vào một nút inline, bot nhận được callback query và xử lý dữ liệu callback để thực hiện hành động cụ thể (như hiển thị danh sách dự án hoặc xác nhận triển khai).

## Event

### Update Type.Message:

Sự kiện khi bot nhận được một tin nhắn mới từ người dùng. Bot sẽ kiểm tra nội dung của tin nhắn để xác định lệnh và thực hiện hành động tương ứng.

### Update Type.CallbackQuery:

Sự kiện khi người dùng nhấn vào một nút inline trong tin nhắn. Bot sẽ xử lý dữ liệu callback để thực hiện các hành động như hiển thị danh sách dự án hoặc yêu cầu xác nhận.

## Command

### /start:

Lệnh khởi tạo bot, gửi thông báo chào mừng và hiển thị các nút lựa chọn (Deploy Project, Help).

### /clear:

Lệnh xóa tất cả các tin nhắn trong cuộc trò chuyện. Bot sẽ gửi thông báo và sau đó xóa từng tin nhắn trong cuộc trò chuyện.

### /help:

Lệnh hiển thị danh sách các lệnh có sẵn trong bot, cung cấp thông tin về cách sử dụng bot.

### Deploy Project:

Khi người dùng chọn tùy chọn triển khai dự án từ nút inline, bot sẽ hiển thị danh sách các dự án và yêu cầu người dùng xác nhận triển khai.

### Confirmation (Yes/No):

Khi người dùng xác nhận triển khai dự án, bot thực hiện hành động triển khai và thông báo kết quả. Nếu người dùng hủy, bot cung cấp tùy chọn để bắt đầu lại.
