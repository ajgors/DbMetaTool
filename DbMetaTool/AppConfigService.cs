using Microsoft.Extensions.Configuration;

namespace DbMetaTool
{
	public class AppConfigService
	{
		private readonly IConfiguration _config;

		public AppConfigService()
		{
			_config = new ConfigurationBuilder()
				.SetBasePath(Directory.GetCurrentDirectory())
				.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
				.Build();
		}

		public DatabaseSettings GetDatabaseSettings()
		{
			var settings = new DatabaseSettings();
			_config.GetSection("DatabaseCredentials").Bind(settings);
			return settings;
		}

		public string GetConnectionString(string dbPath)
		{
			var dbSettings = GetDatabaseSettings();
			return dbSettings.BuildConnectionString(dbPath);
		}
	}

	public class DatabaseSettings
	{
		public string Username { get; set; } = "SYSDBA";
		public string Password { get; set; } = "masterkey";
		public string DataSource { get; set; } = "localhost";
		public int Port { get; set; } = 3050;

		public string BuildConnectionString(string dbPath)
		{
			return $"User={Username};Password={Password};Database={dbPath};DataSource={DataSource};Port={Port};";
		}
	}
}
