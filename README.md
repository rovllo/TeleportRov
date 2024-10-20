# Telegram to Eitaa and Bale Message Forwarder

## Description

This application serves as an automated bridge between Telegram, Eitaa, and Bale messaging platforms. It monitors a specified Telegram channel and forwards all incoming messages to designated channels on Eitaa and Bale, ensuring seamless content synchronization across these platforms.

## Features

- Forwards text messages, photos, videos, audio files, and documents
- Supports album forwarding (multiple photos in a single message)
- Handles message captions and maintains them during forwarding
- Restricts usage to authorized admin users only
- Provides console logging for monitoring and debugging

## Supported Message Types

- Text messages
- Photos (including albums)
- Videos
- Audio files
- Documents

## Prerequisites

- .NET 6.0 or later
- Telegram Bot API Token
- Eitaa API Token
- Bale API Token
- Channel IDs or usernames for destination channels on Eitaa and Bale

## Setup

1. Clone this repository:
   ```
   git clone https://github.com/yourusername/telegram-eitaa-bale-forwarder.git
   ```

2. Navigate to the project directory:
   ```
   cd telegram-eitaa-bale-forwarder
   ```

3. Open the `config.cf` file and replace the placeholder values with your actual API tokens and channel identifiers:
   ```csharp
   var telegramBotToken = "YOUR_TELEGRAM_BOT_TOKEN";
   eitaaApiToken = "YOUR_EITAA_API_TOKEN";
   eitaaChannelIdentifier = "YOUR_EITAA_CHANNEL_IDENTIFIER";
   baleBotToken = "YOUR_BALE_BOT_TOKEN";
   baleDestinationChannelId = YOUR_BALE_CHANNEL_ID;
   ```

4. Update the `admins` list with the Telegram user IDs of authorized administrators:
   ```csharp
   static readonly List<long> admins = [YOUR_ADMIN_ID_1, YOUR_ADMIN_ID_2, YOUR_ADMIN_ID_3];
   ```

## Running the Application

1. Build the project:
   ```
   dotnet build
   ```

2. Run the application:
   ```
   dotnet run
   ```

The application will start and begin listening for messages in the specified Telegram channel.

## Usage

Once the application is running:

1. Send a message to the Telegram channel that the bot is monitoring.
2. The application will automatically forward the message to the specified Eitaa and Bale channels.
3. Check the console for logs and any error messages.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Disclaimer

This project is not officially associated with Telegram, Eitaa, or Bale. Use it at your own risk and ensure you comply with the terms of service of all platforms involved.
