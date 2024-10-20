/// <summary>
/// Manages the configuration settings for the application.
/// </summary>
class ConfigurationManager
{
	/// <summary>
	/// List of admin user IDs.
	/// </summary>
	public List<long> Admins { get; set; }

	/// <summary>
	/// Telegram bot token.
	/// </summary>
	public string TelegramBotToken { get; set; }

	/// <summary>
	/// Eitaa API token.
	/// </summary>
	public string EitaaApiToken { get; set; }

	/// <summary>
	/// Eitaa channel identifier.
	/// </summary>
	public string EitaaChannelIdentifier { get; set; }

	/// <summary>
	/// Bale bot token.
	/// </summary>
	public string BaleBotToken { get; set; }

	/// <summary>
	/// Bale destination channel ID.
	/// </summary>
	public long BaleDestinationChannelId { get; set; }

	/// <summary>
	/// Loads the configuration from a file.
	/// </summary>
	/// <param name="filePath">The path to the configuration file.</param>
	/// <returns>A ConfigurationManager instance with the loaded settings.</returns>
	public static ConfigurationManager LoadConfiguration(string filePath)
	{
		var lines = File.ReadAllLines(filePath);
		var config = new ConfigurationManager
		{
			Admins = new List<long>()
		};

		foreach (var line in lines)
		{
			var parts = line.Split('=', 2);
			if (parts.Length != 2) continue;

			var key = parts[0].Trim();
			var value = parts[1].Trim();

			switch (key.ToLower())
			{
				case "admins":
					config.Admins = value.Split(',').Select(long.Parse).ToList();
					break;
				case "telegrambottoken":
					config.TelegramBotToken = value;
					break;
				case "eitaaapitoken":
					config.EitaaApiToken = value;
					break;
				case "eitaachannelidentifier":
					config.EitaaChannelIdentifier = value;
					break;
				case "balebottoken":
					config.BaleBotToken = value;
					break;
				case "baledestinationchannelid":
					config.BaleDestinationChannelId = long.Parse(value);
					break;
			}
		}

		return config;
	}
}