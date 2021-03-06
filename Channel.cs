﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace CogitoMini {
	internal enum ChannelMode : int { chat = 0, ads, both }
	internal enum UnderageReponse { Kick, Warn, Alert, Ignore }

	public struct Incident {
		public readonly string Subject;
		public readonly DateTime Time;
		public readonly string Details;

		public override string ToString() { return string.Format("<{0}> [{1}]: {2}", Time.ToString("yyyy-MM-dd HH:mm:ss"), Subject, Details); }

		public Incident(string subject, DateTime time, string details) {
			Subject = subject;
			Time = time;
			Details = details;
		}
	}

	/// <summary>Channel class, for both public and private channels</summary>
	[Serializable]
	internal class Channel : IComparable, IDisposable {
		[NonSerialized]
		private bool disposed = false;
		[NonSerialized]
		internal bool isJoined = false;
		/// <summary> EXPERIMENTAL - contains the last fragment for Autocompletion </summary>
		[NonSerialized]
		internal string lastSearchFragment = "";
		[NonSerialized]
		internal HashSet<User> Mods = new HashSet<User>();
		[NonSerialized]
		internal HashSet<User> Users = new HashSet<User>();
		[NonSerialized]
		internal IO.Logging.LogFile ChannelLog = null;
		[NonSerialized]
		internal IO.Logging.LogFile ChannelModLog = null;
		[NonSerialized]
		internal int joinIndex = -1;
		[NonSerialized]
		internal Queue<Incident> modMessageQueue = new Queue<Incident>();

		[OnDeserialized]
		private void SetValuesOnDeserialized(StreamingContext context) {
			Mods = new HashSet<User>();
			Users = new HashSet<User>();
			modMessageQueue = new Queue<Incident>();
		}

		internal HashSet<string> Whitelist = new HashSet<string>();

		//internal bool hasCustomSettings = false;
		public int userCount { get { return Math.Max(_userCount, Users.Count); } set { userCount = value; } }
		private int _userCount;
		internal object dataLock;

		/// <summary>Keys are the UUID for private channels; channel title for normal. Always use .key for channel-specific commands.</summary>
		private string _key = null;
		internal string Key {
			get { return _key ?? Name; }
			set { _key = value; }
		}

		/// <summary>Channel name, in human-readable format</summary>
		public string Name;

		/// <summary> Channel mode - chat only, ads only, both. </summary>
		internal ChannelMode mode = ChannelMode.both;

		/// <summary>Minimum age to be in this channel. If set to a value greater than 0, the bot will attempt to kick everyone below this age.</summary>
		internal short minAge {
			get { return _minAge; }
			set {
				if (value < 0) { _minAge = 0; underageResponse = Config.AppSettings.DefaultResponse; }
				if (value == 0) { _minAge = 0; underageResponse = Config.AppSettings.DefaultResponse; }
				else { _minAge = value; }
			}
		}
		private short _minAge;

		internal UnderageReponse underageResponse = UnderageReponse.Ignore;

		internal string Description;

		internal void Join() {
			IO.SystemCommand JoinCmd = new IO.SystemCommand();
			JoinCmd.OpCode = "JCH";
			JoinCmd.Data["channel"] = Key;
			JoinCmd.Send();
			isJoined = true;
			InitializeChannelLogs();
		}

		internal void InitializeChannelLogs() {
			if (ChannelLog == null) { ChannelLog = new IO.Logging.LogFile(Name, subdirectory: Name, timestamped: true); }
			if (ChannelModLog == null) { ChannelModLog = new IO.Logging.LogFile(Name + "_Mods", writeInterval: 1000, subdirectory: Name, timestamped: true); }
		}

		internal void Leave() {
			IO.SystemCommand LeaveCmd = new IO.SystemCommand();
			LeaveCmd.OpCode = "LCH";
			LeaveCmd.Data["channel"] = Key;
			LeaveCmd.Send();
			isJoined = false;
			dataLock = false;
			ChannelLog.Dispose();
			ChannelModLog.Dispose();
			//CogitoUI.chatUI.chatTabs.EnsureNotVisible(chanTab);
		}

		/// <summary>
		/// Constructor, used with Public (e.g. name-only) channels
		/// </summary>
		/// <param name="_name">The channel's name</param>
		public Channel(string _name) {
			Key = null;
			Name = _name;
			Core.channels.Add(this);
		}

		public Channel(string _name, int __userCount) : this(_name) { _userCount = __userCount; }

		public Channel(string _name, string _key, int __userCount) : this(_key, _name) { _userCount = __userCount; }

		/// <summary>
		/// Constructor for private channel, used on Invite or after ORS
		/// </summary>
		/// <param name="_key">The channel's UUID, used to join it</param>
		/// <param name="_name">The channel's name</param>
		public Channel(string _key, string _name = "[Private Channel]") : this(_name) { Key = _key; } 

		internal void MessageReceived(IO.Message m) {
			InitializeChannelLogs();
			ChannelLog.Log(m);
			if (m.sourceUser.IsChannelOp(this)) { ChannelModLog.Log(m, true); }
		}

		internal void MessageReceived(string s) {
			InitializeChannelLogs();
			string _s = string.Format("<{0}> -- {1}{2}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), s, Environment.NewLine);
			ChannelLog.LogRaw(_s);
		}

		/// <summary> Implementation of IDispose - removes tab page and disposes of Log to ensure buffer is flushed </summary>
		public void Dispose() {
			if (!disposed) {
				while (modMessageQueue.Count > 0) { ChannelModLog.Log("Unresolved issue at shutdown: " + modMessageQueue.Dequeue().ToString()); }
				if (ChannelLog != null) { ChannelLog.Dispose(); }
				if (ChannelModLog != null) { ChannelModLog.Dispose(); }
			}
			disposed = true;
		}

		/// <summary>Generic destrutor, empties ModMessageQueue</summary>
		~Channel() { Dispose();  }

		public override string ToString() {
			//return _key == null ? String.Format("{0}", Name) : String.Format("{0} ({1})", Name, Key);
			return _key == null ? Name : string.Format("{0} ({1})", Name, Key);
		}

		public override bool Equals(object obj) {
			if (obj == null) { return false; }
			if (this.Key == ((Channel)obj).Key) { return true; }
			//Two channels can have the same name, but never the same key.
			else { return false; }
		}

		public override int GetHashCode() { return Key.GetHashCode(); }

		public bool Equals(Channel channel) {
			if (Name == channel.Name) { return true; }
			else { return false; }
		}

		public int CompareTo(object obj) {
			if (obj == null) { return 1; }
			Channel c = obj as Channel;
			if (c != null) { return Key.CompareTo(c.Key); }
			else { throw new ArgumentException("Object cannot be made into Channel. Cannot CompareTo()."); }

		}

		public void Log(string s, bool suppressPrint = false) { ChannelLog.Log(s, suppressPrint); }
		public void Log(IO.Message m, bool suppressPrint = false) { ChannelLog.Log(m, suppressPrint); }

		public void Kick(User u) { u.Kick(this); }
		public void Ban(User u) { u.Ban(this); }

		public void TryAlertMod(string subject, string message) {
			List<User> modsToAlert = Mods.Where(n => (n.Ignore == false && n.Status < Status.busy)).Intersect(Users).ToList();
			modsToAlert = modsToAlert.Where(n => n.Name != Core.XMLConfig["character"]).ToList();
			#if DEBUG
				Console.WriteLine(string.Format("Userlist: {0}.\nModlist: {1}.\nMods To Alert:\n{2}", string.Join(", ", Users.Select(x => x.Name)), string.Join(", ", Mods.Select(x => x.Name)), string.Join(", ", modsToAlert.Select(n=>n.Name))));
			#endif
			if (modsToAlert.Count == 0) { modMessageQueue.Enqueue(new Incident(subject, DateTime.Now, message)); }
			else {Utils.Math.RandomChoice(modsToAlert).Message(string.Format("This is an automated message.\n\t Subject: {0}\nChannel: {1}\n{2}", subject, Name, message)); }
		}

		public void Message(string message) {
			IO.Message m = new IO.Message();
			m.sourceChannel = this;
			m.Send();
		}

		internal async void CheckAge(string Username) {
			if (Whitelist.Contains(Username)) { return; }
			User u = Core.getUser(Username);
			if ((DateTime.Now - u.dataTakenOn) >= Config.AppSettings.userProfileRefreshPeriod) { await u.GetBasicProfileInfo(); }
			//await GetBasicProfileInfo();
			if (u.Age <= 0 && underageResponse != UnderageReponse.Ignore) {
				int ChannelInteger = Core.joinedChannels.Contains(this) ? Core.joinedChannels.IndexOf(this) : -1;
				if (ChannelInteger == -1) { Core.ErrorLog.Log("Target Channel " + Name + " is not registered in Core.joinedChannels and should not be being monitored..."); return; }
				TryAlertMod(Name, string.Format("Cannot parse age of User '{0}' joining channel '{1}' (minimum age set to {2}). Please verify.\n\tIn case of false positive, please respond with '.whitelist {3} {4} {5}'. Copy and paste the command from this message to avoid misspelling.", u.Name, Name, minAge, u.Name, Config.AppSettings.RedirectOperator, ChannelInteger));
				if (u.Age == -1) { Core.ErrorLog.Log("Age check failed for user " + u.Name + "; check subroutine."); return; }
				return;
			}
			if (u.Age < minAge) {
				switch (underageResponse) {
					default:
					case UnderageReponse.Alert:
						TryAlertMod(Name, string.Format("[b][color=Red]Warning![/color][/b] User '{0}' is below minimum age ({1}) for channel '{2}'. Auto-Kick is disabled. Please proceed manually.", u.Name, minAge, Name));
						break;

					case UnderageReponse.Ignore:
						break;

					case UnderageReponse.Kick:
						u.Message(string.Format("This is an automated message. You, user '{0}', are below the minimum age ({1}) for channel '{2}' and will be removed. You are welcome to join with an of-age character. Have a nice day.", u.Name, minAge, Name));
						Kick(u);
						break;

					case UnderageReponse.Warn:
						Message(string.Format("This is an automated message. User '{0}' is below the minimum age ({1}) for channel '{2}'. Please leave or return with an of-age character.", u.Name, minAge, Name));
						//c.TryAlertMod(Name, string.Format("[b]Warning![/b] User '{0}' is below minimum age ({1}) for channel '{2}'. User has been warned.", Name, c.minAge, c.Name));
						break;
				}
			}
		}
	}
}
