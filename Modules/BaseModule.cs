﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Newtonsoft.Json.Linq;

namespace tModloaderDiscordBot.Modules
{
	[Name("default")]
	public class BaseModule : ConfigModuleBase<SocketCommandContext>
	{
		public BaseModule(CommandService commandService) : base(commandService)
		{
		}

		/// <summary>
		/// Returns bot response time
		/// </summary>
		[Command("ping")]
		[Summary("Returns the bot response time")]
		[Remarks("ping")]
		public async Task Ping([Remainder] string rem = null)
		{
			var sw = Stopwatch.StartNew();
			var d = 60d / Context.Client?.Latency * 1000;
			if (d != null)
			{
				var l = Context.Client.Latency;
				var msg = await ReplyAsync(
					$"My heartrate is ``{(int)d}`` bpm ({l} ms)");
				await msg.ModifyAsync(p => p.Content =
					$"{msg.Content}" +
					$"\nMessage response time is {sw.ElapsedMilliseconds} ms" +
					$"\n[Delta: {Math.Abs(sw.ElapsedMilliseconds - l)} ms]");
				sw.Stop();
			}

		}

		/// <summary>
		/// Generates a mod widget
		/// </summary>
		[Command("widget")]
		[Alias("widgetimg", "widgetimage")]
		[Summary("Generates a widget image of specified mod")]
		[Remarks("widget <mod>\nwidget examplemod")]
		public async Task Widget([Remainder]string mod)
		{
			mod = mod.RemoveWhitespace();
			var (result,str) = await ShowSimilarMods(mod);

			if (result)
			{
				var modFound = ModsManager.Mods.FirstOrDefault(x => x.EqualsIgnoreCase(mod));

				if (modFound != null)
				{
					var msg = await ReplyAsync($"Generating widget for {modFound}...");

					// need perfect string.

					using (var client = new System.Net.Http.HttpClient())
					{
						var response = await client.GetByteArrayAsync($"{ModsManager.WidgetUrl}{modFound}.png");
						using (var stream = new MemoryStream(response))
						{
							await Context.Channel.SendFileAsync(stream, $"widget-{modFound}.png");
						}
					}
					await msg.DeleteAsync();
				}
			}

		}

		/// <summary>
		/// Get mod info
		/// </summary>
		[Command("mod")]
		[Alias("modinfo")]
		[Summary("Shows info about a mod")]
		[Remarks("mod <internal modname> --OR-- mod <part of name>\nmod examplemod")]
		[Priority(-99)]
		public async Task Mod([Remainder] string mod)
		{
			mod = mod.RemoveWhitespace();

			if (mod.EqualsIgnoreCase(">count"))
			{
				await ReplyAsync($"Found `{ModsManager.Mods.Count()}` cached mods");
				return;
			}

			var (result, str) = await ShowSimilarMods(mod);

			if (result)
			{
				if (string.IsNullOrEmpty(str))
				{
					// Fixes not finding files
					mod = ModsManager.Mods.FirstOrDefault(m => string.Equals(m, mod, StringComparison.CurrentCultureIgnoreCase));
					if (mod == null)
						return;
				}
				else mod = str;

				// Some mod is found continue.
				var modjson = JObject.Parse(await BotUtils.FileReadToEndAsync(new SemaphoreSlim(1, 1), ModsManager.ModPath(mod)));
				var eb = new EmbedBuilder()
					.WithTitle("Mod: ")
					.WithCurrentTimestamp()
					.WithAuthor(new EmbedAuthorBuilder
					{
						IconUrl = Context.Message.Author.GetAvatarUrl(),
						Name = $"Requested by {Context.Message.Author.FullName()}"
					});

				foreach (var property in modjson.Properties().Where(x => !string.IsNullOrEmpty(x.Value.ToString())))
				{
					var name = property.Name;
					var value = property.Value;

					if (name.EqualsIgnoreCase("displayname"))
					{
						eb.Title += value.ToString();
					}
					else if (name.EqualsIgnoreCase("downloads"))
					{
						eb.AddField("# of Downloads", $"{property.Value:n0}", true);
					}
					else if (name.EqualsIgnoreCase("updatetimestamp"))
					{
						eb.AddField("Last updated", DateTime.Parse($"{property.Value}").ToString("dddd, MMMMM d, yyyy h:mm:ss tt", new CultureInfo("en-US")), true);
					}
					else if (name.EqualsIgnoreCase("iconurl"))
					{
						eb.ThumbnailUrl = value.ToString();
					}
					else
					{
						eb.AddField(name.FirstCharToUpper(), value, true);
					}
				}

				eb.AddField("Widget", $"<{ModsManager.WidgetUrl}{mod}.png>", true);
				using (var client = new System.Net.Http.HttpClient())
				{
					var response = await client.GetAsync(ModsManager.QueryHomepageUrl + mod);
					var postResponse = await response.Content.ReadAsStringAsync();
					if (!string.IsNullOrEmpty(postResponse) && !postResponse.StartsWith("Failed:"))
					{
						eb.Url = postResponse;
						eb.AddField("Homepage", $"<{postResponse}>", true);
					}
				}

				await ReplyAsync("", embed: eb.Build());
			}
		}

		// Helper method
		private async Task<(bool,string)> ShowSimilarMods(string mod)
		{
			var mods = ModsManager.Mods.Where(m => string.Equals(m, mod, StringComparison.CurrentCultureIgnoreCase));

			if (mods.Any()) return (true, string.Empty);
			var cached = await ModsManager.TryCacheMod(mod);
			if (cached) return (true, string.Empty);

			const string msg = "Mod with that name doesn\'t exist";
			var modMsg = "\nNo similar mods found..."; ;

			// Find similar mods

			var similarMods =
				ModsManager.Mods
					.Where(m => m.Contains(mod, StringComparison.CurrentCultureIgnoreCase)
								&& m.LevenshteinDistance(mod) <= m.Length - 2) // prevents insane amount of mods found
					.ToArray();

			if (similarMods.Any())
			{
				if (similarMods.Length == 1) return (true, similarMods.First());

				modMsg = "\nDid you possibly mean any of these?\n" + similarMods.PrettyPrint();
				// Make sure message doesn't exceed discord's max msg length
				if (modMsg.Length > 2000)
				{
					modMsg = modMsg.Cap(2000 - msg.Length);
					// Make sure message doesn't end with a half cut modname
					var index = modMsg.LastIndexOf(',');
					var lastModClean = modMsg.Substring(index + 1).Replace("`", "").Trim();
					if (ModsManager.Mods.All(m => m != lastModClean))
						modMsg = modMsg.Substring(0, index);
				}
			}

			await ReplyAsync($"{msg}{modMsg}");
			return (false, string.Empty);
		}

		

		//[Command("modcompile")]
		//public async Task ModCompile(string src)
		//{
		//	Uri uriResult;
		//	bool result = Uri.TryCreate(src, UriKind.Absolute, out uriResult)
		//	              && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

		//	if (!result || !src.EndsWith(".zip"))
		//	{
		//		await ReplyAsync("Given is not URL. You must send a .zip file of the mod source. Make sure it is not in a separate directory.");
		//		return;
		//	}

		//	UpdateModule.Bash($@"rm -rf modcompiles/{Context.User.Id}");
		//	UpdateModule.Bash($@"mkdir modcompiles/{Context.User.Id}");
		//	UpdateModule.Bash($@"wget {src} -P modcompiles/{Context.User.Id}/");
		//	UpdateModule.Bash($@"unzip modcompiles/{Context.User.Id}/*.zip -d modcompiles/{Context.User.Id}/");
		//	UpdateModule.Bash($@"rm modcompiles/{Context.User.Id}/*.zip");
		//	string compile = UpdateModule.Bash($"\"/home/jofairden/tmlbot/tml/tModLoaderServer.bin.x86_64\" -build \"modcompiles/{Context.User.Id}/\"");
		//	await ReplyAsync(compile);
		//	UpdateModule.Bash($@"rm -rf modcompiles/{Context.User.Id}");
		//	await ReplyAsync("Done?");
		//}

	}
}
