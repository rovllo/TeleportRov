using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using System.Text;
using Newtonsoft.Json.Linq;


/// <summary>
/// Main program class that manages receiving messages from Telegram and sending them to Eitaa and Bale.
/// </summary>
class Program
{
	// Static variables for bot configuration and operation
	static List<long> admins;
	static ITelegramBotClient telegramBot;
	static string eitaaApiToken;
	static string eitaaChannelIdentifier;

	static HttpClient baleClient = new HttpClient();
	static string baleBotToken;
	static readonly string baleBaseUrl = "https://tapi.bale.ai/";
	static long baleDestinationChannelId;
	static string photoPath;

	static ConfigurationManager config;

	/// <summary>
	/// Entry point of the program.
	/// </summary>
	static async Task Main()
	{
		try
		{
			// Load configuration from file
			LoadConfiguration();

			// Initialize Telegram bot client
			telegramBot = new TelegramBotClient(config.TelegramBotToken);

			// Set up directory for temporary file storage
			string executionPath = AppDomain.CurrentDomain.BaseDirectory;
			photoPath = Path.Combine(executionPath, "files");
			if (!Directory.Exists(photoPath))
			{
				Directory.CreateDirectory(photoPath);
			}

			// Set up cancellation token source for bot operation
			using var cts = new CancellationTokenSource();
			var receiverOptions = new Telegram.Bot.Polling.ReceiverOptions
			{
				AllowedUpdates = Array.Empty<UpdateType>()
			};

			// Start receiving updates from Telegram
			telegramBot.StartReceiving(
				updateHandler: HandleUpdateAsync,
				pollingErrorHandler: HandlePollingErrorAsync,
				receiverOptions: receiverOptions,
				cancellationToken: cts.Token
			);

			// Display bot information
			var me = await telegramBot.GetMeAsync();
			Console.WriteLine($"Start listening for @{me.Username}");
			Console.WriteLine("Press any key to stop the bot...");
			Console.ReadKey();

			// Stop receiving updates
			cts.Cancel();
		}
		catch (Exception ex)
		{
			Console.WriteLine($"An error occurred while starting the bot: {ex.Message}");
			Console.WriteLine("Press any key to exit...");
			Console.ReadKey();
		}
	}

	/// <summary>
	/// Loads configuration from the config file.
	/// </summary>
	static void LoadConfiguration()
	{
		string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.cf");
		if (!System.IO.File.Exists(configPath))
		{
			throw new FileNotFoundException("Configuration file not found", configPath);
		}

		config = ConfigurationManager.LoadConfiguration(configPath);

		// Assign configuration values to static variables
		admins = config.Admins;
		eitaaApiToken = config.EitaaApiToken;
		eitaaChannelIdentifier = config.EitaaChannelIdentifier;
		baleBotToken = config.BaleBotToken;
		baleDestinationChannelId = config.BaleDestinationChannelId;

		Console.WriteLine("Configuration loaded successfully.");
	}

	/// <summary>
	/// Handles incoming updates from Telegram.
	/// </summary>
	static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
	{
		// Check if the update has a message
		if (update.Message is not { } message)
			return;

		// Check if the sender is an admin
		if (!admins.Contains(update.Message.From.Id))
		{
			await botClient.SendTextMessageAsync(update.Message.From.Id, "You don't have access...", cancellationToken: cancellationToken);
			return;
		}

		var chatId = message.Chat.Id;
		Console.WriteLine($"Received a message in chat {chatId}.");

		try
		{
			// Process the message based on its type
			switch (message.Type)
			{
				case MessageType.Text:
					await SendMessageToEitaa(message.Text);
					await SendMessageToBale(message.Text);
					break;
				case MessageType.Photo:
					if (message.MediaGroupId != null)
					{
						await HandleAlbumAsync(botClient, chatId, message.MediaGroupId, cancellationToken);
					}
					else
					{
						await SendFileToEitaa(message.Photo[^1].FileId, "photo.jpg", message.Caption);
						await SendPhotoToBale(message);
					}
					break;
				case MessageType.Video:
					await SendFileToEitaa(message.Video.FileId, "video.mp4", message.Caption);
					await SendVideoToBale(message);
					break;
				case MessageType.Audio:
					await SendFileToEitaa(message.Audio.FileId, "audio.mp3", message.Caption);
					await SendAudioToBale(message);
					break;
				case MessageType.Document:
					await SendFileToEitaa(message.Document.FileId, message.Document.FileName, message.Caption);
					await SendDocumentToBale(message);
					break;
				default:
					await SendMessageToEitaa("Unsupported message type received.");
					await SendMessageToBale("Unsupported message type received.");
					break;
			}

			await botClient.SendTextMessageAsync(chatId, "Your message has been sent to Eitaa and Bale.", cancellationToken: cancellationToken);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error processing message: {ex.Message}");
			await botClient.SendTextMessageAsync(chatId, "An error occurred while sending your message to Eitaa or Bale.", cancellationToken: cancellationToken);
		}
	}

	/// <summary>
	/// Handles album messages (multiple photos sent as a group).
	/// </summary>
	static async Task HandleAlbumAsync(ITelegramBotClient botClient, long chatId, string mediaGroupId, CancellationToken cancellationToken)
	{
		// Wait for all messages in the album to be received
		await Task.Delay(1000, cancellationToken);

		var messages = await botClient.GetUpdatesAsync(cancellationToken: cancellationToken);
		var albumMessages = messages
			.Select(u => u.Message)
			.Where(m => m.MediaGroupId == mediaGroupId)
			.ToList();

		if (albumMessages.Any())
		{
			var caption = albumMessages.FirstOrDefault()?.Caption ?? "Album";
			var fileIds = new List<string>();

			foreach (var albumMessage in albumMessages)
			{
				if (albumMessage.Photo != null)
				{
					var photo = albumMessage.Photo[^1];
					await SendFileToEitaa(photo.FileId, $"photo_{photo.FileId}.jpg", null);
					await SendPhotoToBale(albumMessage);
					fileIds.Add(photo.FileId);
				}
			}

			await SendMessageToEitaa($"Album: {caption}\n{string.Join("\n", fileIds)}");
			await SendMessageToBale($"Album: {caption}\n{string.Join("\n", fileIds)}");
		}
	}

	/// <summary>
	/// Handles errors that occur during message polling.
	/// </summary>
	static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
	{
		var ErrorMessage = exception switch
		{
			ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
			_ => exception.ToString()
		};

		Console.WriteLine(ErrorMessage);
		return Task.CompletedTask;
	}

	/// <summary>
	/// Sends a text message to Eitaa.
	/// </summary>
	static async Task SendMessageToEitaa(string message)
	{
		var payload = new MultipartFormDataContent
		{
			{ new StringContent(GetFormattedChannelIdentifier(eitaaChannelIdentifier)), "chat_id" },
			{ new StringContent(message), "text" }
		};

		await SendRequestToEitaa("sendMessage", payload);
	}

	/// <summary>
	/// Sends a file to Eitaa.
	/// </summary>
	static async Task SendFileToEitaa(string fileId, string fileName, string caption)
	{
		var fileContent = await DownloadTelegramFile(fileId);
		var payload = new MultipartFormDataContent
		{
			{ new StringContent(GetFormattedChannelIdentifier(eitaaChannelIdentifier)), "chat_id" },
			{ new ByteArrayContent(fileContent), "file", fileName }
		};

		if (!string.IsNullOrEmpty(caption))
		{
			payload.Add(new StringContent(caption), "caption");
		}

		await SendRequestToEitaa("sendFile", payload);
	}

	/// <summary>
	/// Sends a request to the Eitaa API.
	/// </summary>
	static async Task SendRequestToEitaa(string method, MultipartFormDataContent payload)
	{
		using var client = new HttpClient();
		var response = await client.PostAsync($"https://eitaayar.ir/api/{eitaaApiToken}/{method}", payload);
		var responseString = await response.Content.ReadAsStringAsync();
		Console.WriteLine($"Eitaa API Response: {responseString}");

		var jsonResponse = JObject.Parse(responseString);
		if (!(bool)jsonResponse["ok"])
		{
			throw new Exception($"Eitaa API error: {responseString}");
		}
	}

	/// <summary>
	/// Sends a text message to Bale.
	/// </summary>
	static async Task SendMessageToBale(string message)
	{
		var endpoint = $"{baleBaseUrl}bot{baleBotToken}/sendMessage";

		var payload = new
		{
			chat_id = baleDestinationChannelId,
			text = message,
		};

		var json = System.Text.Json.JsonSerializer.Serialize(payload);
		var content = new StringContent(json, Encoding.UTF8, "application/json");

		var response = await baleClient.PostAsync(endpoint, content);
		response.EnsureSuccessStatusCode();

		var responseBody = await response.Content.ReadAsStringAsync();
		Console.WriteLine($"Bale API Response: {responseBody}");
	}

	/// <summary>
	/// Sends a photo to Bale.
	/// </summary>
	static async Task SendPhotoToBale(Message message)
	{
		try
		{
			var fileId = message.Photo[^1].FileId;
			var file = await telegramBot.GetFileAsync(fileId);
			var filePath = file.FilePath;
			string distFile = Path.Combine(photoPath, Guid.NewGuid().ToString() + ".jpg");

			using (var saveImageStream = new FileStream(distFile, FileMode.Create))
			{
				await telegramBot.DownloadFileAsync(filePath, saveImageStream);
			}

			await SendFileToChannel(baleDestinationChannelId.ToString(), distFile, "photo", message.Caption ?? "");
			Console.WriteLine("Photo successfully sent to Bale.");

			System.IO.File.Delete(distFile);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error in sending photo to Bale: {ex.Message}");
		}
	}

	/// <summary>
	/// Sends a video to Bale.
	/// </summary>
	static async Task SendVideoToBale(Message message)
	{
		try
		{
			var file = await telegramBot.GetFileAsync(message.Video.FileId);
			var filePath = file.FilePath;
			string distFile = Path.Combine(photoPath, Guid.NewGuid().ToString() + ".mp4");

			using (var saveVideoStream = new FileStream(distFile, FileMode.Create))
			{
				await telegramBot.DownloadFileAsync(filePath, saveVideoStream);
			}

			await SendFileToChannel(baleDestinationChannelId.ToString(), distFile, "video", message.Caption ?? "");
			Console.WriteLine("Video successfully sent to Bale.");

			System.IO.File.Delete(distFile);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error in sending video to Bale: {ex.Message}");
		}
	}

	/// <summary>
	/// Sends an audio file to Bale.
	/// </summary>
	static async Task SendAudioToBale(Message message)
	{
		try
		{
			var file = await telegramBot.GetFileAsync(message.Audio.FileId);
			var filePath = file.FilePath;
			string distFile = Path.Combine(photoPath, Guid.NewGuid().ToString() + ".mp3");

			using (var saveAudioStream = new FileStream(distFile, FileMode.Create))
			{
				await telegramBot.DownloadFileAsync(filePath, saveAudioStream);
			}

			await SendFileToChannel(baleDestinationChannelId.ToString(), distFile, "audio", message.Caption ?? "");
			Console.WriteLine("Audio successfully sent to Bale.");

			System.IO.File.Delete(distFile);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error in sending audio to Bale: {ex.Message}");
		}
	}

	/// <summary>
	/// Sends a document to Bale.
	/// </summary>
	static async Task SendDocumentToBale(Message message)
	{
		try
		{
			var file = await telegramBot.GetFileAsync(message.Document.FileId);
			var filePath = file.FilePath;
			string distFile = Path.Combine(photoPath, message.Document.FileName);

			using (var saveDocumentStream = new FileStream(distFile, FileMode.Create))
			{
				await telegramBot.DownloadFileAsync(filePath, saveDocumentStream);
			}

			await SendFileToChannel(baleDestinationChannelId.ToString(), distFile, "document", message.Caption ?? "");
			Console.WriteLine("Document successfully sent to Bale.");

			System.IO.File.Delete(distFile);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error in sending document to Bale: {ex.Message}");
		}
	}

	/// <summary>
	/// Sends a file to a Bale channel.
	/// </summary>
	static async Task SendFileToChannel(string channelId, string filePath, string fileType, string caption = "")
	{
		try
		{
			var multipartContent = new MultipartFormDataContent();

			var fileContent = new ByteArrayContent(System.IO.File.ReadAllBytes(filePath));
			multipartContent.Add(fileContent, fileType, Path.GetFileName(filePath));

			multipartContent.Add(new StringContent(channelId), "chat_id");
			if (!string.IsNullOrEmpty(caption))
			{
				multipartContent.Add(new StringContent(caption), "caption");
			}

			string endpoint = fileType switch
			{
				"photo" => "sendPhoto",
				"video" => "sendVideo",
				"audio" => "sendAudio",
				"document" => "sendDocument",
				_ => throw new ArgumentException($"Unsupported file type: {fileType}")
			};

			var response = await baleClient.PostAsync($"{baleBaseUrl}bot{baleBotToken}/{endpoint}", multipartContent);

			if (response.IsSuccessStatusCode)
			{
				var result = await response.Content.ReadAsStringAsync();
				Console.WriteLine($"{fileType} successfully sent to Bale.");
				Console.WriteLine($"Response: {result}");
			}
			else
			{
				Console.WriteLine($"Error sending {fileType} to Bale. Status code: {response.StatusCode}");
				var errorContent = await response.Content.ReadAsStringAsync();
				Console.WriteLine($"Error details: {errorContent}");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error sending {fileType} to Bale: {ex.Message}");
		}
	}

	/// <summary>
	/// Formats the channel identifier for use with APIs.
	/// </summary>
	/// <param name="identifier">The channel identifier to format.</param>
	/// <returns>The formatted channel identifier.</returns>
	static string GetFormattedChannelIdentifier(string identifier)
	{
		return long.TryParse(identifier, out _) ? identifier : identifier.TrimStart('@');
	}

	/// <summary>
	/// Downloads a file from Telegram.
	/// </summary>
	/// <param name="fileId">The ID of the file to download.</param>
	/// <returns>The file content as a byte array.</returns>
	static async Task<byte[]> DownloadTelegramFile(string fileId)
	{
		var file = await telegramBot.GetFileAsync(fileId);
		using var stream = new MemoryStream();
		await telegramBot.DownloadFileAsync(file.FilePath, stream);
		return stream.ToArray();
	}
}
