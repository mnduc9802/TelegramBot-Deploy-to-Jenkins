# :arrow_down: ENGLISH BELOW :arrow_down:

# TIẾNG VIỆT
# 🤖Telegram Deploy Bot by mnduc9802
> Một bot Telegram mạnh mẽ để quản lý triển khai Jenkins, được tạo ra bởi @mnduc9802

## 📋 Mục lục 
- Cài đặt quan trọng
- Giới thiệu
- Kiến trúc
- Tính năng
- Lệnh
- Cài đặt
- Cấu hình
- Cách sử dụng
- Ghi chú

## ⚠️ Cài đặt quan trọng
1. Tạo token API bot bằng cách sử dụng @BotFather trên Telegram
2. Tạo file `.env` trong cùng cấp với thư mục với `src/` với cấu trúc sau:
```
//Telegram API
TELEGRAM_BOT_TOKEN=*******

//My Telegram Chat Id (Dùng cho lệnh /feedback)
MY_TELEGRAM_CHAT_ID=*******

//Group Telegram Chat Id (Dùng cho lệnh /notify)
GROUP_TELEGRAM_CHAT_ID=*******

//Jenkins Info
JENKINS_URL=*******

DEVOPS_USERNAME=*******
DEVOPS_PASSWORD=*******

DEVELOPER_USERNAME=*******
DEVELOPER_PASSWORD=*******

TESTER_USERNAME=*******
TESTER_PASSWORD=*******

Dùng một trong hai
//Database Local Info
DATABASE_CONNECTION_STRING=*******

//Database Server Info
DATABASE_CONNECTION_STRING=*******
```


## 📝 Giới thiệu
Bot Telegram này được thiết kế để hỗ trợ triển khai các dự án từ Jenkins. Bot cung cấp một số lệnh để người dùng có thể tương tác và thực hiện các hoạt động như triển khai dự án, xem trạng thái, gửi phản hồi, và nhiều hơn nữa.

## 🏗️ Kiến trúc

### Khái niệm cốt lõi
1. Lệnh: Hướng dẫn dạng văn bản mà người dùng gửi để tương tác với bot
2. Updates: Tin nhắn và sự kiện đến từ Telegram
3. Handlers: Các thành phần chuyên biệt xử lý các loại updates khác nhau
4. Tích hợp Jenkins: Kết nối trực tiếp đến Jenkins để thực hiện các thao tác triển khai

### Thiết kế hệ thống
1. Kiến trúc hướng sự kiện: Bot hoạt động trên mô hình hướng sự kiện
2. Quản lý trạng thái: Theo dõi tương tác người dùng và trạng thái triển khai
3. Xử lý bất đồng bộ: Xử lý nhiều yêu cầu đồng thời
4. Xử lý lỗi: Hệ thống bắt và ghi log lỗi toàn diện

## ✨ Tính năng
- 🚀 Tích hợp triển khai Jenkins
- 📊 Giám sát trạng thái dự án
- ⏰ Lập lịch triển khai
- 🔍 Chức năng tìm kiếm dự án
- 📝 Hệ thống phản hồi người dùng
- 🔔 Thông báo nhóm
- 🧹 Tiện ích dọn dẹp chat

## 🔧 Lệnh
Lệnh | Mô tả
--- | ---
/start | Khởi tạo bot và hiển thị tin nhắn chào mừng
/projects | Hiển thị danh sách dự án và công việc đã lên lịch
/deploy | Bắt đầu quy trình triển khai
/status | Kiểm tra trạng thái bot 
/notify | Gửi thông báo đến nhóm chat
/clear | Dọn dẹp tin nhắn của bot gần đây
/feedback | Gửi phản hồi cho admin bot
/help | Hiển thị trợ giúp lệnh
/myinfo | Hiển thị thông tin người dùng

## 🚀 Cài đặt
### Yêu cầu tiên quyết
- .NET 8.0 SDK trở lên
- Truy cập vào máy chủ Jenkins
- Token bot Telegram
- Máy chủ database PostgreSQL (Tùy chọn)
- Máy chủ Linux cho triển khai production (Tùy chọn)

### Các bước cài đặt
#### Môi trường Development
1. Clone repository:
```
git clone https://github.com/yourusername/telegram-deploy-bot.git
cd telegram-deploy-bot
```

2. Cài đặt dependencies:
```
dotnet restore
```

3. Cấu hình biến môi trường:
- Sao chép `.env.example` thành `.env`
- Điền tất cả các giá trị cần thiết

4. Build dự án:
```
dotnet build
```

5. Chạy bot:
```
dotnet run
```

#### Triển khai trên Server
1. Chuẩn bị môi trường:
- Đảm bảo đã cài đặt DBeaver trên máy local
- Có quyền truy cập vào cả database local và server
- Có thông tin kết nối database server (host, port, username, password)

2. Kết nối Database trong DBeaver:
```
a. Kết nối Local Database:
   - Click chuột phải > Create > Connection
   - Chọn loại database của bạn (MySQL, PostgreSQL, etc.)
   - Điền thông tin kết nối local
   - Test Connection và Save

b. Kết nối Server Database:
   - Click chuột phải > Create > Connection
   - Chọn cùng loại database
   - Điền thông tin kết nối server
   - Test Connection và Save
```

3. Export Database từ Local:
```
a. Backup schema:
   - Click chuột phải vào database local
   - Tools > Database Transfer
   - Chọn "Backup schema" trong Extract Options
   - Chọn đường dẫn lưu file backup

b. Backup dữ liệu:
   - Trong cùng cửa sổ Database Transfer
   - Chọn "Backup data" trong Extract Options
   - Có thể chọn "Plain" hoặc "Native" format
   - Click Start để bắt đầu export
```

4. Import Database lên Server:
```
a. Khôi phục schema:
   - Click chuột phải vào database server
   - Tools > Database Transfer
   - Chọn file backup schema đã export
   - Click Start để khôi phục cấu trúc

b. Khôi phục dữ liệu:
   - Trong cửa sổ Database Transfer
   - Chọn file backup dữ liệu
   - Kiểm tra các tùy chọn import
   - Click Start để import dữ liệu
```

5. Kiểm tra và Xác nhận:
```
a. Kiểm tra cấu trúc:
   - So sánh số lượng bảng
   - Kiểm tra các ràng buộc và indexes
   - Xác nhận các stored procedures và functions

b. Kiểm tra dữ liệu:
   - So sánh số lượng records
   - Kiểm tra dữ liệu mẫu
   - Xác nhận các relationships
```

6. Sử dụng Docker để triển khai trên Server
```
a. Máy Local:
   - docker build -t example.domain.com/organization/project:latest .
   - docker push example.domain.com/organization/project:latest
b. Máy Server:
   - docker pull example.domain.com/organization/project:latest
   - docker run example.domain.com/organization/project:latest
```

## ⚙️ Cấu hình
### Cấu hình Bot
1. Tạo bot mới thông qua @BotFather
2. Lấy token bot và thêm vào .env
3. Thiết lập quyền cần thiết cho bot

### Cấu hình Jenkins
1. Chọn giữa database local hoặc server
2. Cấu hình chuỗi kết nối trong .env
3. Đảm bảo schema database được thiết lập đúng

## 📱 Cách sử dụng
1. Bắt đầu chat với bot của bạn trên Telegram
2. Sử dụng /start để khởi tạo bot
3. Xem các lệnh có sẵn với /help
4. Sử dụng /projects để xem các dự án có sẵn
5. Triển khai dự án bằng /deploy
6. Giám sát trạng thái triển khai với /status

## 📌 Ghi chú
### Bảo mật
- Giữ token bot an toàn
- Thường xuyên thay đổi thông tin đăng nhập Jenkins
- Theo dõi log truy cập

### Hạn chế
- Bot phải có quyền thích hợp trong các nhóm
- Máy chủ Jenkins phải có thể truy cập được
- Yêu cầu kết nối mạng

### Xử lý sự cố
- Kiểm tra log bot để tìm lỗi
- Xác minh kết nối Jenkins
- Đảm bảo cấu hình môi trường đúng
- Theo dõi trạng thái kết nối database

## 👥 Đóng góp
Chào đón mọi đóng góp! Vui lòng gửi Pull Request.

## 🙏 Cảm ơn
- Thư viện Telegram.Bot
- Jenkins API
- Cộng đồng .NET

---

# ENGLISH
# 🤖Telegram Deploy Bot by mnduc9802
> A powerful Telegram bot for managing Jenkins deployments, created by @mnduc9802

## 📋 Table of Contents 
- Important Setup
- Introduction
- Architecture
- Features
- Commands
- Installation
- Configuration
- Usage
- Notes

## ⚠️ Important Setup
1. Create a bot API token using @BotFather on Telegram
2. Create a `.env` file at the same level as the `src/` directory with the following structure:
```
//Telegram API
TELEGRAM_BOT_TOKEN=*******

//My Telegram Chat Id (Dùng cho lệnh /feedback)
MY_TELEGRAM_CHAT_ID=*******

//Group Telegram Chat Id (Dùng cho lệnh /notify)
GROUP_TELEGRAM_CHAT_ID=*******

//Jenkins Info
JENKINS_URL=*******

DEVOPS_USERNAME=*******
DEVOPS_PASSWORD=*******

DEVELOPER_USERNAME=*******
DEVELOPER_PASSWORD=*******

TESTER_USERNAME=*******
TESTER_PASSWORD=*******

Dùng một trong hai
//Database Local Info
DATABASE_CONNECTION_STRING=*******

//Database Server Info
DATABASE_CONNECTION_STRING=*******
```


## 📝 Introduction
This Telegram bot is designed to facilitate project deployments from Jenkins. The bot provides various commands for users to interact and perform operations such as project deployment, status checking, sending feedback, and more.

## 🏗️ Architecture

### Core Concepts
1. Commands: Text-based instructions that users send to interact with the bot
2. Updates: Messages and events coming from Telegram
3. Handlers: Specialized components that process different types of updates
4. Jenkins Integration: Direct connection to Jenkins for deployment operations

### System Design
1. Event-Driven Architecture: Bot operates on an event-driven model
2. State Management: Tracks user interactions and deployment states
3. Asynchronous Processing: Handles multiple concurrent requests
4. Error Handling: Comprehensive error catching and logging system

## ✨ Features
- 🚀 Jenkins deployment integration
- 📊 Project status monitoring
- ⏰ Deployment scheduling
- 🔍 Project search functionality
- 📝 User feedback system
- 🔔 Group notifications
- 🧹 Chat cleanup utilities

## 🔧 Commands
Commands | Description
--- | ---
/start | Initialize bot and display welcome message
/projects | Display list of projects and scheduled jobs
/deploy | Start deployment process
/status | Check bot status
/notify | Send notification to group chat
/clear | Clean up recent bot messages
/feedback | Send feedback to bot admin
/help | Display command help
/myinfo | Display user information

## 🚀  Installation
### Prerequisites
- .NET 8.0 SDK or higher
- Access to Jenkins server
- Telegram bot token
- PostgreSQL database server (Optional)
- Linux server for production deployment (Optional)

### Installation Steps
#### Development Environment
1. Clone repository:
```
git clone https://github.com/yourusername/telegram-deploy-bot.git
cd telegram-deploy-bot
```

2. Install dependencies:
```
dotnet restore
```

3. Configure environment variables:
- Copy `.env.example` to `.env`
- Fill in all required values

4. Build project:
```
dotnet build
```

5. Run bot:
```
dotnet run
```

#### Server Deployment
1. Prepare environment:
- Ensure DBeaver is installed on local machine
- Have access to both local and server databases
- Have server database connection information (host, port, username, password)

2. Connect Database in DBeaver:
```
a. Connect Local Database:
   - Right-click > Create > Connection
   - Choose your database type (MySQL, PostgreSQL, etc.)
   - Fill in local connection details
   - Test Connection and Save

b. Connect Server Database:
   - Right-click > Create > Connection
   - Choose same database type
   - Fill in server connection details
   - Test Connection and Save
```

3. Export Database from Local:
```
a. Backup schema:
   - Right-click on local database
   - Tools > Database Transfer
   - Select "Backup schema" in Extract Options
   - Choose backup file path

b. Backup data:
   - In the same Database Transfer window
   - Select "Backup data" in Extract Options
   - Can choose "Plain" or "Native" format
   - Click Start to begin export
```

4. Import Database to Server:
```
a. Restore schema:
   - Right-click on server database
   - Tools > Database Transfer
   - Select exported schema backup file
   - Click Start to restore structure

b. Restore data:
   - In Database Transfer window
   - Select data backup file
   - Check import options
   - Click Start to import data
```

5. Check and Verify:
```
a. Check structure:
   - Compare number of tables
   - Check constraints and indexes
   - Verify stored procedures and functions

b. Check data:
   - Compare number of records
   - Check sample data
   - Verify relationships
```

6. Use Docker for Server Deployment
```
a. Local Machine:
   - docker build -t example.domain.com/organization/project:latest .
   - docker push example.domain.com/organization/project:latest
b. Server Machine:
   - docker pull example.domain.com/organization/project:latest
   - docker run example.domain.com/organization/project:latest
```

## ⚙️ Configuration
### Bot Configuration
1. Create new bot through @BotFather
2. Get bot token and add to .env
3. Set up necessary permissions for bot

### Jenkins Configuration
1. Choose between local or server database
2. Configure connection string in .env
3. Ensure database schema is properly set up

## 📱 Usage
1. Start chatting with your bot on Telegram
2. Use /start to initialize the bot
3. View available commands with /help
4. Use /projects to see available projects
5. Deploy projects using /deploy
6. Monitor deployment status with /status

## 📌 Notes
### Security
- Keep bot token secure
- Regularly change Jenkins login credentials
- Monitor access logs

### Limitations
- Bot must have appropriate permissions in groups
- Jenkins server must be accessible
- Network connectivity required

### Troubleshooting
- Check bot logs for errors
- Verify Jenkins connection
- Ensure correct environment configuration
- Monitor database connection status

## 👥 Contributing
Contributions are welcome! Please submit Pull Requests.

## 🙏 Acknowledgments
- Telegram.Bot library
- Jenkins API
- .NET Community
