﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using tModloaderDiscordBot.Preconditions;

namespace tModloaderDiscordBot.Modules
{
	[Group("permission")]
	[Alias("perm")]
	[HasPermission]
	public class PermissionModule : ConfigModuleBase<SocketCommandContext>
	{


		public PermissionModule(CommandService commandService) : base(commandService)
		{
		}

		[Group("admin")]
		public class AdminModule : ConfigModuleBase<SocketCommandContext>
		{
			private static bool resetFlag;

			public AdminModule(CommandService commandService) : base(commandService)
			{
			}

			[Command("reset")]
			[ServerOwnerOnly]
			public async Task ResetAsync([Remainder]string rem = "")
			{
				if (!resetFlag)
				{
					await ReplyAsync("You are about to reset all permissions, are you sure you want to continue? Issue the command again within 1 minute to affirm.");
					resetFlag = true;
					await Task.Delay(TimeSpan.FromMinutes(1));
					resetFlag = false;
				}
				else
				{
					int admins = Config.Permissions.Admins.Count;
					Config.Permissions.Admins.Clear();
					int perms = Config.Permissions.Permissions.Count;
					Config.Permissions.Permissions.Clear();
					int blocked = Config.Permissions.Blocked.Count;
					Config.Permissions.Blocked.Clear();
					await Config.Update();
					resetFlag = false;
					await ReplyAsync($"All permission were reset. Removed {admins} admins, {perms} permissions and {blocked} blocks.");
				}
			}

			[Command("assign")]
			[Alias("add", "-a")]
			public async Task AssignAsync(IRole role)
				=> await AssignAsync(role.Id);

			[Command("assign")]
			[Alias("add", "-a")]
			public async Task AssignAsync(IGuildUser user)
				=> await AssignAsync(user.Id);

			[Command("assign")]
			[Alias("add", "-a")]
			public async Task AssignAsync(ulong id)
			{
				if (!Config.Permissions.MakeAdmin(id))
				{
					await ReplyAsync($"`{id}` is already admin.");
					return;
				}

				await Config.Update();
				await ReplyAsync($"`{id}` has been made admin.");
			}

			[Command("unassign")]
			[Alias("delete", "-d", "-ua")]
			public async Task UnassignAsync(IRole role)
				=> await UnassignAsync(role.Id);

			[Command("unassign")]
			[Alias("delete", "-d", "-ua")]
			public async Task UnassignAsync(IGuildUser user)
				=> await UnassignAsync(user.Id);

			[Command("unassign")]
			[Alias("delete", "-d", "-ua")]
			public async Task UnassignAsync(ulong id)
			{
				if (!Config.Permissions.RemoveAdmin(id))
				{
					await ReplyAsync($"`{id}` was not admin.");
					return;
				}

				await Config.Update();
				await ReplyAsync($"`{id}` is no longer admin.");
			}
		}

		[Command]
		[Alias("find", "-f")]
		public async Task Default(string command)
		{
			command = await BotUtils.SearchCommand(CommandService, Context, command);
			if (command == null)
				return;

			if (!Config.Permissions.MapHasPermissionsFor(command))
			{
				await ReplyAsync($"No permissions found for `{command}`");
				return;
			}

			var sb = new StringBuilder();
			sb.AppendLine($"Permissions found for `{command}`");

			foreach (var permission in Config.Permissions.Permissions[command])
				sb.AppendLine($"`{permission}`");

			if (sb.Length >= 1000)
				await ReplyAsync($"I found too many permissions to be listed here. (ERR: Message too long)");
			else
				await ReplyAsync(sb.ToString());
		}

		[Command("set")]
		[Alias("add", "-a")]
		public async Task SetAsync(string command, params ulong[] ids)
		{
			command = await BotUtils.SearchCommand(CommandService, Context, command);
			if (command == null)
				return;

			var msg = await ReplyAsync("Updating permissions...");

			if (!Config.Permissions.MapHasPermissionsFor(command))
				if (!Config.Permissions.NewPermission(command))
				{
					await msg.ModifyAsync(x => x.Content = $"Failed to create new permission");
					return;
				}

			Config.Permissions.Permissions[command] = new HashSet<ulong>(Config.Permissions.Permissions[command].Union(ids));

			await Config.Update();
			await msg.ModifyAsync(x => x.Content = $"Permissions for `{command}` have been updated");
		}

		[Command("remove")]
		[Alias("delete", "-d")]
		public async Task RemoveAsync(string command, params ulong[] ids)
		{
			command = await BotUtils.SearchCommand(CommandService, Context, command);
			if (command == null)
				return;

			var msg = await ReplyAsync("Updating permissions...");

			if (!Config.Permissions.MapHasPermissionsFor(command))
			{
				await msg.ModifyAsync(x => x.Content = $"Permissions for `{command}` were not found.");
				return;
			}

			Config.Permissions.Permissions[command] = new HashSet<ulong>(Config.Permissions.Permissions[command].Except(ids));

			await Config.Update();
			await msg.ModifyAsync(x => x.Content = $"Permissions for `{command}` have been updated");
		}
	}
}
