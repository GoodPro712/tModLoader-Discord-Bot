﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using tModloaderDiscordBot.Utils;

namespace tModloaderDiscordBot.Services
{
	public class ModService : BaseService
	{
		internal const string QueryDownloadUrl = "http://javid.ddns.net/tModLoader/tools/querymoddownloadurl.php?modname=";
		internal const string QueryHomepageUrl = "http://javid.ddns.net/tModLoader/tools/querymodhomepage.php?modname=";
		internal const string WidgetUrl = "http://javid.ddns.net/tModLoader/widget/widgetimage/";
		internal const string XmlUrl = "http://javid.ddns.net/tModLoader/listmods.php";
		internal const string ModInfoUrl = "http://javid.ddns.net/tModLoader/tools/modinfo.php";
		internal const string HomepageUrl = "http://javid.ddns.net/tModLoader/moddescription.php";
		internal const string PopularUrl = "http://javid.ddns.net/tModLoader/tools/populartop10.php";
		internal const string HotUrl = "http://javid.ddns.net/tModLoader/BotUtils/hottop10.php";

		private static string ModDir => "mods";
		internal static string tMLVersion;

		public static string ModPath(string modname) =>
			Path.Combine(ModDir, $"{modname}.json");

		public static IEnumerable<string> Mods =>
			Directory.GetFiles(ModDir, "*.json")
				.Select(x => Path.GetFileNameWithoutExtension(x).RemoveWhitespace());

		private static SemaphoreSlim _semaphore;
		private static Timer _updateTimer;
		private readonly DiscordSocketClient _client;

		public ModService(IServiceProvider services, DiscordSocketClient client) : base(services)
		{
			_client = client;
		}

		public void Initialize()
		{
			tMLVersion = "v0.10.1.5";
			_semaphore = new SemaphoreSlim(1, 1);

			if (_updateTimer == null)
			{
				_updateTimer = new Timer(async (e) =>
				{
					await Maintain(_client);
				},
				null, TimeSpan.FromHours(6), TimeSpan.FromHours(6));
			}
		}

		public async Task Maintain(IDiscordClient client)
		{
			await Log("Starting maintenance");
			// Create dirs
			Directory.CreateDirectory(ModDir);

			var path = Path.Combine(ModDir, "date.txt");
			var dateDiff = TimeSpan.MinValue;

			// Data.txt present, read
			if (File.Exists(path))
			{
				var savedBinary = await FileUtils.FileReadToEndAsync(_semaphore, path);
				var parsedBinary = long.Parse(savedBinary);
				var savedBinaryDate = BotUtils.DateTimeFromUnixTimestampSeconds(parsedBinary);
				dateDiff = BotUtils.DateTimeFromUnixTimestampSeconds(BotUtils.GetCurrentUnixTimestampSeconds()) - savedBinaryDate;
				await Log($"Read date difference: {dateDiff}");
			}

			// Needs to maintain data
			if (dateDiff == TimeSpan.MinValue
				|| dateDiff.TotalHours > 5.99d)
			{
				await Log($"Maintenance determined: over 6 hours. Updating...");
				var data = await DownloadData();
				var modlist = JObject.Parse(data).SelectToken("modlist").ToObject<JArray>();

				foreach (var jtoken in modlist)
				{
					var name = jtoken.SelectToken("name").ToObject<string>().RemoveWhitespace();
					var jsonPath = Path.Combine(ModDir, $"{name}.json");
					await FileUtils.FileWriteAsync(_semaphore, jsonPath, jtoken.ToString(Formatting.Indented));
				}

				await FileUtils.FileWriteAsync(_semaphore, path, BotUtils.GetCurrentUnixTimestampSeconds().ToString());
				await Log($"File write successful");
			}
		}

		public async Task<bool> TryCacheMod(string name)
		{
			await Log($"Attempting to update cache for mod {name}");
			var data = await DownloadSingleData(name);
			if (data.StartsWith("Failed:"))
			{
				await Log($"Cache update for mod {name} failed");
				return false;
			}

			var mod = JObject.Parse(data);
			await FileUtils.FileWriteAsync(_semaphore, Path.Combine(ModDir, $"{mod.SelectToken("name").ToObject<string>()}.json"), mod.ToString(Formatting.Indented));
			await Log($"Cache update for mod {name} succeeded");
			return true;
		}

		private async Task<string> DownloadData()
		{
			await Log($"Requesting DownloadData. tMod version: {tMLVersion}");
			using (var client = new System.Net.Http.HttpClient())
			{
				//var version = await GetTMLVersion();

				var values = new Dictionary<string, string>
				{
					{ "modloaderversion", $"tModLoader {tMLVersion}" },
					{ "platform", "w"}
				};
				var content = new System.Net.Http.FormUrlEncodedContent(values);

				await Log("Sending post request");
				var response = await client.PostAsync(XmlUrl, content);

				await Log("Reading post request");
				var postResponse = await response.Content.ReadAsStringAsync();

				await Log("Done downloading data");
				return postResponse;
			}
		}

		private async Task<string> DownloadSingleData(string name)
		{
			using (var client = new System.Net.Http.HttpClient())
			{
				var response = await client.GetAsync(ModInfoUrl + $"?modname={name}");
				var postResponse = await response.Content.ReadAsStringAsync();
				return postResponse;
			}
		}

		private async Task Log(string msg)
		{
			await _loggingService.Log(new LogMessage(LogSeverity.Info, "ModService", msg));
		}
	}
}
