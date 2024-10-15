using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Polling;
using TelegramBot.Core.Handlers;
using TelegramBot.Commands.Minor;
using TelegramBot.Services;
using TelegramBot.Utilities.Environment;

namespace TelegramBot.Core
{
    public class Program
    {
        #region Fields and Properties
        public static ITelegramBotClient botClient { get; private set; }
        public static string? connectionString { get; private set; }
        public static string? botToken { get; private set; }
        #endregion

        #region Main Method
        public static async Task Main(string[] args)
        {
            try
            {
                await RunAsync();
            }
            catch (Exception ex)
            {
                ErrorHandler.HandleCriticalError(ex);
            }
        }

        private static async Task RunAsync()
        {
            InitializeServices();
            await StartBot();
            await RunBot();
        }
        #endregion

        #region Initialization
        private static void InitializeServices()
        {
            LoggerService.Initialize();
            LoggerService.LogInformation($"Starting Telegram Bot... Log file location: {LoggerService.LogFilePath}");

            string jenkinsUrl = EnvironmentVariableLoader.GetJenkinsUrl();
            botToken = EnvironmentVariableLoader.GetTelegramBotToken();
            connectionString = EnvironmentVariableLoader.GetDatabaseConnectionString();

            if (string.IsNullOrEmpty(botToken))
            {
                LoggerService.LogError(new Exception("Bot token is missing"), "Bot token is not configured");
                throw new InvalidOperationException("Bot token is missing");
            }
        }

        private static async Task StartBot()
        {
            botClient = new TelegramBotClient(botToken);

            try
            {
                var me = await botClient.GetMeAsync();
                LoggerService.LogInformation($"Bot started successfully. Bot Username: {me.Username}");
            }
            catch (Exception ex)
            {
                LoggerService.LogError(ex, "Failed to initialize bot client");
                throw;
            }
        }

        private static async Task RunBot()
        {
            await MenuCommand.SetBotCommandsAsync(botClient);
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>(),
                ThrowPendingUpdates = true
            };

            botClient.StartReceiving(UpdateHandler.HandleUpdateAsync, ErrorHandler.HandlePollingErrorAsync, receiverOptions);

            JobService.Initialize();
            LoggerService.LogInformation("Bot is running. Press any key to exit.");
            Console.WriteLine($"Bot is running. Logs are being written to: {LoggerService.LogFilePath}");
            Console.Read();
        }
        #endregion
    }
}