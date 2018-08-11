﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace tModloaderDiscordBot.Configs
{
	public sealed class PermissionsConfig : ICloneable, IEquatable<PermissionsConfig>
	{
		public IDictionary<string, ISet<ulong>> Permissions;
		public ISet<ulong> Admins;
		public ISet<ulong> Blocked;

		public object Clone()
		{
			var clone = (PermissionsConfig)this.MemberwiseClone();
			clone.Permissions = Permissions != null ? new Dictionary<string, ISet<ulong>>(Permissions) : new Dictionary<string, ISet<ulong>>();
			clone.Admins = Admins != null ? new HashSet<ulong>(Admins) : new HashSet<ulong>();
			clone.Blocked = Blocked != null ? new HashSet<ulong>(Blocked) : new HashSet<ulong>();
			return clone;
		}

		public bool ValidataData()
		{
			var clone = (PermissionsConfig)Clone();

			if (Permissions == null)
				Permissions = new Dictionary<string, ISet<ulong>>();

			Permissions =
				Permissions
				.Where(x => x.Value.Count > 0)
				.ToDictionary(x => x.Key, x => x.Value);

			if (Admins == null)
				Admins = new HashSet<ulong>();

			Admins =
				Admins
				.Where(x => x != default(ulong))
				.ToHashSet();

			if (Blocked == null)
				Blocked = new HashSet<ulong>();

			Blocked =
				Blocked
				.Where(x => x != default(ulong))
				.ToHashSet();

			return clone == this;
		}

		public bool NewPermission(LowerInvariantString str)
		{
			if (MapHasPermissionsFor(str))
				return false;

			Permissions.Add(str.GetText(), new HashSet<ulong>());
			return true;
		}

		public bool MakeAdmin(ulong userId)
			=> Admins.Add(userId);

		public bool RemoveAdmin(ulong userId)
			=> Admins.Remove(userId);

		public bool MapHasPermissionsFor(LowerInvariantString str)
			=> Permissions.ContainsKey(str.GetText());

		public bool IsBlocked(ulong userId)
			=> Blocked.Contains(userId);

		public bool IsAdmin(ulong userId)
			=> Admins.Contains(userId);

		public bool HasPermission(LowerInvariantString str, ulong id)
			=> IsAdmin(id)
			|| !IsBlocked(id)
				&& MapHasPermissionsFor(str)
				&& Permissions[str.GetText()].Contains(id);

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			return obj is PermissionsConfig config && Equals(config);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = (Permissions != null ? Permissions.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (Admins != null ? Admins.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (Blocked != null ? Blocked.GetHashCode() : 0);
				return hashCode;
			}
		}

		public static bool operator ==(PermissionsConfig config1, PermissionsConfig config2)
		{
			return EqualityComparer<PermissionsConfig>.Default.Equals(config1, config2);
		}

		public static bool operator !=(PermissionsConfig config1, PermissionsConfig config2)
		{
			return !(config1 == config2);
		}

		public bool Equals(PermissionsConfig other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return Equals(Permissions, other.Permissions) && Equals(Admins, other.Admins) && Equals(Blocked, other.Blocked);
		}
	}
}
