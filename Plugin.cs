﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CogitoMini.IO;

namespace CogitoMini {
	public enum AccessLevel : byte { Everyone = 0, ChannelOps, GlobalOps, RootOnly }
	public enum AccessPath : byte { All = 0, ChannelOnly, PMOnly }

	internal interface IHost {
		void LogToErrorFile(string s);
		void LogToSystemFile(string s);
		//ConcurrentSet<User> GetGlobalUserList();
		HashSet<User> GetChannelUserList(Channel c);
		HashSet<User> GetChannelModList(Channel c);
	}
	internal interface IPlugin {
		string Name { get; }                //Plugin Name, e.g. "Dice roll"
		string Description { get; }         //Description to be given when a list is requested
		string Trigger { get; }             //trigger, e.g. ".roll", to be registered in Config.AITriggers
		AccessLevel AccessLevel { get; }    //The level of access required to execute this command
		AccessPath AccessPath { get; }      //The avenues through which this command may be executed
		void MessageLoopMethod(Message m);  //Method to be executed on EACH message received (just pass if not needed)
		void ShutdownMethod();				 //Method to be executed when program shuts down,	e.g. saving data
		void SetupMethod();					//Method to be executed when program starts,		e.g. loading data 
		void PluginMethod(Message m);       //The method that's actually executed
		IHost Host { get; }
	}
	abstract class CogitoPlugin : IPlugin {
		public abstract string Name { get; }
		public abstract string Description { get; }
		public abstract string Trigger { get; }
		public abstract AccessLevel AccessLevel { get; }
		public abstract AccessPath AccessPath { get; }
		public abstract void MessageLoopMethod(Message m);
		public abstract void ShutdownMethod();
		public abstract void SetupMethod();
		public abstract void PluginMethod(Message m);
		public abstract IHost Host { get; }
	}

	static class Plugins {
		internal static Dictionary<string, CogitoPlugin> PluginStore = new Dictionary<string, CogitoPlugin>();

		internal static void LoadPlugins() {
			List<string> dllFileNames = new List<string>();
			if (Directory.Exists(Config.AppSettings.PluginsPath)) { dllFileNames.AddRange(Directory.GetFiles(Config.AppSettings.PluginsPath, "*.dll")); }
			if (dllFileNames.Count > 0) {
				Type CSPluginType = typeof(CogitoPlugin);
				ICollection<Type> CSPluginTypes = new HashSet<Type>();
				foreach (string filename in dllFileNames) {
					Assembly a = Assembly.LoadFile(filename);
					if (a != null) {
						Type[] types = a.GetTypes();
						foreach (Type t in types) {
							if (t.IsInterface || t.IsAbstract) { continue; }
							else { if (t.GetInterface(CSPluginType.FullName) != null) { CSPluginTypes.Add(t); } }
						}   //foreach Type t
					}   // a != null
				} //foreach filename
				foreach (Type _t in CSPluginTypes) {
					CogitoPlugin plugin = (CogitoPlugin)Activator.CreateInstance(_t);
					if (!PluginStore.ContainsKey(plugin.Name)) {                        //no duplicates, no overwrites.
						PluginStore.Add(plugin.Name, plugin); //registers name -> Plugin
						Config.AITriggers.Add(plugin.Trigger, plugin);
					}
					else { Core.ErrorLog.Log("Attempted to load duplicate plugin, type '" + plugin.Name + "'."); }
				}
			}
			AddInternalPlugin<Admin>();
			AddInternalPlugin<Horoscopes>();
			AddInternalPlugin<Ignore>();
			AddInternalPlugin<ListChannels>();
			AddInternalPlugin<Minage>();
			AddInternalPlugin<ModQueue>();
			AddInternalPlugin<RainbowText>();
			AddInternalPlugin<Remote>();
			AddInternalPlugin<Scan>();
			AddInternalPlugin<Shutdown>();
			AddInternalPlugin<Whitelist>();
			AddInternalPlugin<YouTube>();
		}
		private static void AddInternalPlugin<T>() where T: CogitoPlugin, new() {
			try {
				T Plugin = new T();
				PluginStore.Add(Plugin.Name, Plugin);
				Config.AITriggers.Add(Plugin.Trigger, Plugin);
				Plugin.SetupMethod();
				Core.SystemLog.Log("Plugin '" + Plugin.Name +"' successfully loaded and trigger '" + Plugin.Trigger + "' registered.");
            }
			catch (Exception e) {
				Core.SystemLog.Log("Error whilst loading Plugin of type '" + typeof(T).Name + "': \n" + e.Message);
			}
		}

		sealed class Admin : CogitoPlugin {
			public override string Name { get { return "Adminstration Tools"; } }
			public override string Description { get { return "Allows execution of channel Op commands via Cogito"; } }
			public override string Trigger { get { return (Config.AppSettings.TriggerPrefix + "op"); } }
			public override AccessLevel AccessLevel { get { return AccessLevel.Everyone; } }
			public override AccessPath AccessPath { get { return AccessPath.All; } }

			public override void MessageLoopMethod(Message m) { return; }
			public override void ShutdownMethod() { return; }
			public override void SetupMethod() { return; }

			public override IHost Host { get { return Core.pluginHost; } }

			public string[] sass = { "NERF DIS!", "JUSTICE RAINS FROM ABOVE!", "Dòng zhù! Bùxǔ zǒu!", "The target’s threat judgement has been reappraised. Enforcement mode: Lethal eliminator. Trigger safety released. Aim carefully and eliminate the target.", "Do you feel lucky, punk?" };

			public override void PluginMethod(Message m) {
				if (m.sourceChannel == null) { m.Reply("No source channel attached to command. Either use command from within a channel, or use the redirect operator '" + Config.AppSettings.RedirectOperator + "' plus a channel ID number starting at 0 (obtained using .ls) to select the correct channel."); return; }
				bool modeKick = m.Args.Contains("-k") ? true : false;
				m.Args = m.Args.Where(n => n != "-k").ToArray();
				bool modeBan = m.Args.Contains("-b") ? true : false;
				m.Args = m.Args.Where(n => n != "-b").ToArray();
				bool modeTimeout = m.Args.Contains("-t") ? true : false;
				m.Args = m.Args.Where(n => n != "-t").ToArray();
				bool modeSass = m.Args.Contains("-s") ? true : false;
				m.Args = m.Args.Where(n => n != "-s").ToArray();
				//bool modeOp = m.args.Contains("-op") ? true : false;
				//m.args = m.args.Where(n => n != "-op").ToArray();
				//bool modeDeop = m.args.Contains("-deop") ? true : false;
				//m.args = m.args.Where(n => n != "-deop").ToArray();
				int timeoutLength = -1;
				if (m.Args.Length < 1) { m.Reply("Not enough arguments supplied. Need at least mode and target name."); return; }
				if (modeTimeout && int.TryParse(m.Args[0], out timeoutLength)) { m.Args = m.Args.Skip(1).ToArray(); }
				string TargetUser = m.Body, sassMessage = "";
				if ((modeKick ? 1 : 0) + (modeBan ? 1 : 0) + (modeTimeout ? 1 : 0) > 1) { m.Reply("One mode only!", IO.ReplyMode.ForcePM); return; }
				if (!m.sourceChannel.Users.Select(x => x.Name).Contains(TargetUser)) { m.Reply("Target '" + TargetUser + "' is not currently in channel '" + m.sourceChannel.Name + "'. Unable to comply."); return; }
				User target = m.sourceChannel.Users.Where(x => x.Name == TargetUser).First();
				if (modeKick) {
					sassMessage = "Subject " + TargetUser + ". Enforcement mode: Channel Kick. Please aim calmly and subdue the target.";
					target.Kick(m.sourceChannel);
				}
				if (modeBan) {
					sassMessage = "Subject " + TargetUser + ". The target’s threat judgement has been reappraised. Enforcement mode: Channel Ban. Trigger safety released. Aim carefully and eliminate the target.";
					target.Ban(m.sourceChannel);
				}
				if (modeTimeout) {
					sassMessage = string.Format("Subject {0}. Enforcement mode: Time out for {1} {2}. Please aim calmly and subdue the target.", TargetUser, timeoutLength, timeoutLength == 1 ? "minute" : "minutes");
					target.Timeout(m.sourceChannel, timeoutLength);
				}
				if (modeSass) { m.Reply(sassMessage, IO.ReplyMode.ForceChannel); }
			}

		}// plugin Admin
		sealed class Dictionary : CogitoPlugin {
			internal struct AutoDictItem {
				internal DateTime LastUsed;
				internal string Key;
				internal string Definition;
			}

			public override string Name { get { return "Auto-Dictionary"; } }
			public override string Description { get { return "You keep using that word. I do not think it means what you think it means"; } }
			public override string Trigger { get { return (Config.AppSettings.TriggerPrefix + "dict"); } }
			public override AccessLevel AccessLevel { get { return AccessLevel.Everyone; } }
			public override AccessPath AccessPath { get { return AccessPath.All; } }

			internal AutoDictItem[] dictData;
			private bool doAutoDict;

			public override void MessageLoopMethod(Message m) {
				//TDOO timeout first (global) or per-word?
				//TODO if any word in message is in dict, check if timeout still active, if  if () {  }
			}
			public override void ShutdownMethod() { }
			public override void SetupMethod() {
				FListProcessor.ChatMessage += MessageLoopMethod;
				Core.SystemLog.Log("Registered Loop Method of Plugin '" + Name + "...");
				string[] dictDataStrs = File.ReadAllText(Config.AppSettings.DataPath + "dictionary.txt").Split('\n');
				Array.Resize(ref dictData, dictDataStrs.Length);
				for (int i = 0; i<dictDataStrs.Length; i++) {
					AutoDictItem adi = new AutoDictItem();
					string[] s2 = dictDataStrs[i].Split('|');
					adi.Key = s2[0];
					adi.Definition = s2[1];
					dictData[i] = adi;
				}
				Core.SystemLog.Log("Dictionary Database loaded. Why aren't I using XML??");
			}

			public override IHost Host { get { return Core.pluginHost; } }

			public override void PluginMethod(Message m) {
				bool toggleAutoDict = m.Args.Contains("-a") ? true : false;
				//m.args = m.args.Where(x => x != "-a").ToArray();
				if (toggleAutoDict) {
					doAutoDict = !doAutoDict;
					m.Reply(string.Format("Fully automated dictionary: {0}", doAutoDict ? "enabled" : "disabled"));
				}
			}

		}// plugin Dictionary
		sealed class Horoscopes : CogitoPlugin {
			public override string Name { get { return "Celestial Guidance Extrapolation"; } }
			public override string Description { get { return "Blessed Child, heed the wisdom of the stars. You idiot."; } }
			public override string Trigger { get { return (Config.AppSettings.TriggerPrefix + "hs"); } }
			public override AccessLevel AccessLevel { get { return AccessLevel.Everyone; } }
			public override AccessPath AccessPath { get { return AccessPath.All; } }

			public override void MessageLoopMethod(Message m) { return; }
			public override void ShutdownMethod() { return; }
			public override void SetupMethod() { horoscopes = File.ReadAllText(Config.AppSettings.DataPath + "horoscopes.txt").Split('\n'); }

			public override IHost Host { get { return Core.pluginHost; } }

			string[] months = { "january", "february", "march", "april", "may", "june", "july", "august", "september", "october", "november", "december" };
			string[] days = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
			string[] elements = { "Hydrogen", "Helium", "Lithium", "Beryllium", "Boron", "Carbon", "Nitrogen", "Oxygen", "Fluorine", "Neon", "Sodium", "Magnesium", "Aluminum", "Silicon", "Phosphorus", "Sulfur", "Chlorine", "Argon", "Potassium", "Calcium", "Scandium", "Titanium", "Vanadium", "Chromium", "Manganese", "Iron", "Cobalt", "Nickel", "Copper", "Zinc", "Gallium", "Germanium", "Arsenic", "Selenium", "Bromine", "Krypton", "Rubidium", "Strontium", "Yttrium", "Zirconium", "Niobium", "Molybdenum", "Technetium", "Ruthenium", "Rhodium", "Palladium", "Silver", "Cadmium", "Indium", "Tin", "Antimony", "Tellurium", "Iodine", "Xenon", "Cesium", "Barium", "Hafnium", "Tantalum", "Tungsten", "Rhenium", "Osmium", "Iridium", "Platinum", "Gold", "Mercury", "Thallium", "Lead", "Bismuth", "Polonium", "Astatine", "Radon", "Francium", "Radium", "Unnilquadium", "Unnilpentium", "Unnilhexium", "Unnilseptium", "Unniloctium", "Unnilennium", "Ununnilium", "Unununium", "Ununbium", "Lanthanum", "Cerium", "Praseodymium", "Neodymium", "Promethium", "Samarium", "Europium", "Gadolinium", "Terbium", "Dysprosium", "Holmium", "Erbium", "Thulium", "Ytterbium", "Lutetium", "Actinium", "Thorium", "Protactinium", "Uranium", "Neptunium", "Plutonium", "Americium", "Curium", "Berkelium", "Californium", "Einsteinium", "Fermium", "Mendelevium", "Nobelium", "Lawrencium" };

			Dictionary<int, int> mon_len = new Dictionary<int, int> { { 1, 0 }, { 2, 32 }, { 3, 60 }, { 4, 91 }, { 5, 121 }, { 6, 152 }, { 7, 182 }, { 8, 213 }, { 9, 243 }, { 10, 274 }, { 11, 305 }, { 12, 335 } };

			Dictionary<int, string> signs = new Dictionary<int, string> { { 20, "Capricorn" }, { 50, "Aquarius" }, { 79, "Pisces" }, { 110, "Aries" }, { 141, "Taurus" }, { 172, "Gemini" }, { 202, "Cancer" }, { 234, "Leo" }, { 266, "Virgo" }, { 296, "Libra" }, { 326, "Scorpio" }, { 356, "Sagittarius" }, { 366, "Capricorn" } };

			string[] horoscopes;

			public override void PluginMethod(Message m) {
				int day = 0;
				int month = 0;
				System.Text.RegularExpressions.Match DateSearch = Utils.RegularExpressions.Dates.Match(m.Body);
				day = DateSearch.Groups["day"].Success ? int.Parse(DateSearch.Groups["day"].Value) : 0;
				if (day == 0 || day > 31) { m.Reply("Invalid day format; unable to parse date. Please try again with e.g. .hs dd.mm", IO.ReplyMode.AsOriginal); return; }
				if (DateSearch.Groups["month"].Success && !int.TryParse(DateSearch.Groups["month"].Value, out month)) {
					string _month = DateSearch.Groups["month"].Value.ToLowerInvariant();
					if (months.Contains(_month)) { month = Array.IndexOf(months, _month) + 1; }
				}
				if (month == 0) { m.Reply("Invalid month format; unable to parse date. Please try again with e.g. .hs dd.mm", IO.ReplyMode.AsOriginal); return; }
				if (month > 12) {
					int i = day;
					day = month;
					month = i;
				}

				day = day + mon_len[month];
				string sign = signs[signs.Keys.SkipWhile(n => n < day).First()];
				string horoscope;
				Utils.Math.Shuffle(horoscopes);
				horoscope = "[" + sign + "]: " + horoscopes[0] + " " + horoscopes[1];
				if (m.sourceChannel != null) {
					string[] _horoscope = horoscope.Split(' ');
					horoscope = string.Join(" ", _horoscope.Select(x => x=="{NAME}" ? Utils.Math.RandomChoice(m.sourceChannel.Users).Name : x)); 
				}
				else { horoscope = horoscope.Replace("{NAME}", Utils.Math.RandomChoice(Core.globalOps).Name); }
				horoscope = horoscope.Replace("{DAY}", Utils.Math.RandomChoice(days));
				horoscope = horoscope.Replace("{ELEMENT}", Utils.Math.RandomChoice(elements));
				horoscope = horoscope.Replace("{SIGN}", sign);
				string rsign = sign;
				while (rsign == sign) { rsign = Utils.Math.RandomChoice(signs.Values); }
				horoscope = horoscope.Replace("{RSIGN}", rsign);
				m.Reply(horoscope, IO.ReplyMode.AsOriginal);
			}

		}// plugin horoscope
		sealed class Ignore : CogitoPlugin {
			public override string Name { get { return "Opt-out"; } }
			public override string Description { get { return "Allows channel moderators to opt out of receiving Cogito notifications."; } }
			public override string Trigger { get { return (Config.AppSettings.TriggerPrefix + "ignoreme"); } }
			public override AccessLevel AccessLevel { get { return AccessLevel.ChannelOps; } }
			public override AccessPath AccessPath { get { return AccessPath.PMOnly; } }

			public override void MessageLoopMethod(Message m) { }
			public override void ShutdownMethod() { }
			public override void SetupMethod() { }

			public override IHost Host { get { return Core.pluginHost; } }

			public override void PluginMethod(Message m) {
				m.sourceUser.Ignore = !m.sourceUser.Ignore;
				if (!Core.allGlobalUsers.Contains(m.sourceUser)) { Core.allGlobalUsers.Add(m.sourceUser); }
				m.Reply(string.Format("Command received. You are now {0} ignored and {1} be receiving notifications for any channels you moderate and we survey.\nThank you for using Cogito. We are always watching.", m.sourceUser.Ignore ? "being" : "no longer being", m.sourceUser.Ignore ? "won't" : "will"));
			}

		}// plugin Ignore
		sealed class ListChannels : CogitoPlugin {
			public override string Name { get { return "Channel List"; } }
			public override string Description { get { return "Returns the names and command indices of all currently joined channels"; } }
			public override string Trigger { get { return (Config.AppSettings.TriggerPrefix + "ls"); } }
			public override AccessLevel AccessLevel { get { return AccessLevel.Everyone; } }
			public override AccessPath AccessPath { get { return AccessPath.PMOnly; } }

			public override void MessageLoopMethod(Message m) { return; }
			public override void ShutdownMethod() { return; }
			public override void SetupMethod() { return; }

			public override IHost Host { get { return Core.pluginHost; } }

			public override void PluginMethod(Message m) {

				bool modeJoin = m.Args.Contains("-j") ? true : false;
				m.Args = m.Args.Where(n => n != "-j").ToArray();

				bool modeLeave = m.Args.Contains("-l") ? true : false;
				m.Args = m.Args.Where(n => n != "-l").ToArray();

				if (modeJoin && modeLeave) { m.Reply("One mode only, please!"); }

				if (modeJoin || modeLeave) {
					string targetChannel = m.Body;
				}
				else {
					string reply = "Currently joined channels, ordered by their Redirect Key, are:\n";
					for (int i = 0; i < Core.joinedChannels.Count; i++) { reply += string.Format("\t{0}: '{1}' [{2}]\n", i, Core.joinedChannels[i].Name, Core.joinedChannels[i].Key); }
					m.Reply(reply, IO.ReplyMode.ForcePM);
				}
			}

		}// plugin ListChannel
		sealed class Minage : CogitoPlugin {
			public override string Name { get { return "Age Verification"; } }
			public override string Description { get { return "Modifies automatic age check settings"; } }
			public override string Trigger { get { return (Config.AppSettings.TriggerPrefix + "minAge"); } }
			public override AccessLevel AccessLevel { get { return AccessLevel.ChannelOps; } }
			public override AccessPath AccessPath { get { return AccessPath.All; } }

			public override void MessageLoopMethod(Message m) { return; }
			public override void ShutdownMethod() { return; }
			public override void SetupMethod() { return; }

			public override IHost Host { get { return Core.pluginHost; } }

			public override void PluginMethod(Message m) {
				if (m.sourceChannel != null) {
					if (m.Args.Contains("-r")) {
						int optIndex = Array.IndexOf(m.Args, "-r");
						if (optIndex + 1 <= m.Args.Length) {
							UnderageReponse newResponse = new UnderageReponse();
							if (Enum.TryParse<UnderageReponse>(m.Args[optIndex + 1], out newResponse)) { m.sourceChannel.underageResponse = newResponse; }
						}
					}
					if (m.Args.Contains("-a")) {
						short oldMinAge = m.sourceChannel.minAge;
						int optIndex = Array.IndexOf(m.Args, "-a");
						if (short.TryParse(m.Args[optIndex+1], out short newMinAge)) {
							m.sourceChannel.minAge = newMinAge;
							string changeMsg = string.Format("Minimum age for '{0}' changed from '{1}' to '{2}' by '{3}' ({4}). Enforcement mode: {5}", m.sourceChannel, oldMinAge, newMinAge, m.sourceUser, m.AccessLevel, m.sourceChannel.underageResponse);
							m.sourceChannel.ChannelModLog.Log(changeMsg);
						}
						else { m.Reply("Cannot parse '" + m.Args[optIndex+1] + "' as number. Expected format: .minage <newMinAge> (-r Kick|Ignore|Warn|Alert)"); }
					}
					if (m.Args.Contains("-t")) { m.sourceChannel.TryAlertMod("Mod Alert Test", "This is a test message, please disregard. Thank you for your cooperation."); }
					else {
						m.Reply(string.Format("Settings for channel '{0}': Control system {1}. Minimum age: {2}. Enforcement mode: {3}.", m.sourceChannel.Name, m.sourceChannel.minAge > 0 ? "enabled" : "disabled", m.sourceChannel.minAge, m.sourceChannel.underageResponse), IO.ReplyMode.AsOriginal);
					}
				}
				try { Core.SaveAllSettingsBinary(); } //Core.SaveAllSettingsXML();
				catch (Exception e) { Core.ErrorLog.Log(string.Format("Failed to save user data after changing settings for channel {0}, args {1} - {2}", m.sourceChannel, m.Args, e.StackTrace)); }
			}
		}// plugin minage
		sealed class ModQueue : CogitoPlugin {
			public override string Name { get { return "Incident Reports"; } }
			public override string Description { get { return "On request, returns all relevant incidents to the moderator in question"; } }
			public override string Trigger { get { return (Config.AppSettings.TriggerPrefix + "modQueue"); } }
			public override AccessLevel AccessLevel { get { return AccessLevel.Everyone; } }
			public override AccessPath AccessPath { get { return AccessPath.PMOnly; } }

			public override void MessageLoopMethod(Message m) { }
			public override void ShutdownMethod() { }
			public override void SetupMethod() { }

			public override IHost Host { get { return Core.pluginHost; } }

			public override void PluginMethod(Message m) { FListProcessor.ProcessModMessageQueue(m.sourceUser); }

		}// plugin ModQueue
		sealed class RainbowText : CogitoPlugin {
			public override string Name { get { return "Rainbow Text"; } }
			public override string Description { get { return "Guess."; } }
			public override string Trigger { get { return (Config.AppSettings.TriggerPrefix + "r"); } }
			public override AccessLevel AccessLevel { get { return AccessLevel.Everyone; } }
			public override AccessPath AccessPath { get { return AccessPath.All; } }

			public override void MessageLoopMethod(Message m) { return; }
			public override void ShutdownMethod() { return; }
			public override void SetupMethod() { return; }

			public override IHost Host { get { return Core.pluginHost; } }

			public override void PluginMethod(Message m) {
				if (m.Body.Length < 7) { m.Reply("That's waaaaaay too short to make a rainbow out of."); }
				string[] Chunked = Utils.StringManipulation.Chunk(7, m.Body);
				if (Chunked.Length != 7) { Console.WriteLine("INVALID RAINBOW CHUNKS - " + Chunked.ToString()); return; }
				m.Reply(string.Format("[color=red]{0}[/color][color=orange]{1}[/color][color=yellow]{2}[/color][color=green]{3}[/color][color=cyan]{4}[/color][color=blue]{5}[/color][color=purple]{6}[/color]", Chunked[0], Chunked[1], Chunked[2], Chunked[3], Chunked[4], Chunked[5], Chunked[6]), IO.ReplyMode.AsOriginal);
			}
		}// plugin Rainbowtext
		sealed class Remote : CogitoPlugin {
			public override string Name { get { return "Remote Interface"; } }
			public override string Description { get { return "Allows acting through Cogito"; } }
			public override string Trigger { get { return (Config.AppSettings.TriggerPrefix + "ri"); } }
			public override AccessLevel AccessLevel { get { return AccessLevel.ChannelOps; } }
			public override AccessPath AccessPath { get { return AccessPath.PMOnly; } }

			public override void MessageLoopMethod(Message m) { return; }
			public override void ShutdownMethod() { return; }
			public override void SetupMethod() { return; }

			public override IHost Host { get { return Core.pluginHost; } }

			public override void PluginMethod(Message m) {
				bool modeRaw = m.Args.Contains("-r") ? true : false;
				m.Args = m.Args.Where(n => n != "-r").ToArray();
				bool modeSay = m.Args.Contains("-s") ? true : false;
				m.Args = m.Args.Where(n => n != "-s").ToArray();
				bool modeAct = m.Args.Contains("-a") ? true : false;
				m.Args = m.Args.Where(n => n != "-a").ToArray();
				bool modePrivate = m.Args.Contains("-p") ? true : false;
				m.Args = m.Args.Where(n => n != "-p").ToArray();
				if ((modeRaw ? 1 : 0) + (modeSay ? 1 : 0) + (modeAct ? 1 : 0) + (modePrivate ? 1 : 0) > 1) { m.Reply("One mode only, please!", ReplyMode.ForcePM); return; }
				if (modePrivate) {
					Message m2 = new Message(m) {
						Recipient = Core.getUser(m.Args[0]),
						Args = m.Args.Skip(1).ToArray(),
						OpCode = "PRI"
					};
					m2.Send();
				}
				if (modeRaw) { Core.websocket.Send(m.Body); return; }
				if (modeAct) { m.Reply("/me " + m.Body, IO.ReplyMode.ForceChannel); return; }
				else { m.Reply(m.Body, ReplyMode.ForceChannel); }
			}

		}// plugin Remote
		sealed class Scan : CogitoPlugin {
			public override string Name { get { return "Channel Scanner"; } }
			public override string Description { get { return "Gathers data from all users currently present in the channel"; } }
			public override string Trigger { get { return (Config.AppSettings.TriggerPrefix + "scan"); } }
			public override AccessLevel AccessLevel { get { return AccessLevel.ChannelOps; } }
			public override AccessPath AccessPath { get { return AccessPath.ChannelOnly; } }

			public override void MessageLoopMethod(Message m) { return; }
			public override void ShutdownMethod() { return; }
			public override void SetupMethod() { return; }
			public override IHost Host { get { return Core.pluginHost; } }

			public async override void PluginMethod(Message m) {
				bool modeAnonymous = m.Args.Contains("-a") ? true : false; //Are max and min named? Requires modeStatistic
				bool modeStatistic = m.Args.Contains("-s") ? true : false;
				bool modeGreater = m.Args.Contains("-gt") ? true : false;
				bool modeLesser = m.Args.Contains("-lt") ? true : false;
				m.Args = m.Args.Where(n => n != "-a").ToArray();
				m.Args = m.Args.Where(n => n != "-s").ToArray();

				float cutoffValue = 0f;

				if (modeGreater && modeLesser) { m.Reply("Cannot scan for both GreaterThan and LesserThan. Decide."); return; }
				if (modeAnonymous && !modeStatistic) { m.Reply("Deactivating mode anonymous is only supported with mode statistic."); return; }
				if ((modeLesser || modeGreater) && !modeStatistic) { modeStatistic = true; }
				if (modeLesser || modeGreater) {
					int cutoffPos = Array.IndexOf(m.Args, modeLesser ? "-lt" : "-gt");
					if (cutoffPos >= 0) { 
						cutoffValue = float.Parse(m.Args[cutoffPos + 1]);
						m.Args = m.Args.Take(Math.Max(cutoffPos - 1, 0)).Concat(m.Args.Skip(cutoffPos + 1)).ToArray(); //Danger Will Robinson! why not just... idk, look for recognised args and make 'em a list or something? Switch statement.
					}
				}

				List<User> dataPool = m.sourceChannel.Users.ToList();
				Dictionary<string, string> data = new Dictionary<string, string>(dataPool.Count);
				string results;

				Console.WriteLine("Preparing to execute Task Pool...");
				Task DataScan = Task.WhenAll(dataPool.Select(n => n.GetBasicProfileInfo()));
				await DataScan;
				Console.WriteLine("Awaited Task Pool.");
				data = dataPool.ToDictionary(x => x.Name, x => x.TryGetData(m.Body));

				if (modeStatistic) { //transform all to numeric
					DataScan numData = await ProcessNumericData(data);
					if (numData.values.Count == 0) { m.Reply("Could not find/parse any numeric data for input '" + m.Body + "'."); return; }
					//TODO Instead of simpled regex, then float.parse, do utils.math.measurementFromNumber if you ever code it. (As if).
					if (modeLesser || modeGreater) {
						int fullCount = numData.values.Count;
						int lesserCount = numData.values.Values.Where(x => x <= cutoffValue).Count();
						results = string.Format(CultureInfo.InvariantCulture, "Comparative analysis complete. {0:0} of {1:0} users successfully parsed for '{2}'. {3} users had no value set, {4} failed parsing.\n{5:0} users have values {6} specified threshold of {7:0.0}.", fullCount, dataPool.Count, m.Body, numData.nullcount, numData.errorcount, modeLesser ? lesserCount : fullCount - lesserCount, modeLesser ? "below" : "above", cutoffValue);
					}
					else {
						float mean = numData.values.Values.Average();
						//double? median = Utils.Math.Median(numData.Keys);
						float _min = numData.values.Values.Min();
						string min = modeAnonymous ? _min.ToString("0.0", CultureInfo.InvariantCulture) + " [" + string.Join(", ", numData.values.Where(n => n.Value == _min).Select(n => n.Key)).TrimEnd(new char[] { ' ', ',' }) + "]" : _min.ToString("0.0", CultureInfo.InvariantCulture);
						float _max = numData.values.Values.Max();
						string max = modeAnonymous ? _max.ToString("0.0", CultureInfo.InvariantCulture) + " [" + string.Join(", ", numData.values.Where(n => n.Value == _max).Select(n => n.Key)).TrimEnd(new char[] { ' ', ',' }) + "]" : _max.ToString("0.0", CultureInfo.InvariantCulture);
						double stDev = Utils.Math.StDev(numData.values.Values);
						results = string.Format(CultureInfo.InvariantCulture, "Statistical analysis complete. {0:0} of {1:0} users successfully parsed for '{2}'. {3} users had no value set, {4} failed parsing.\nMin: {5:0.0}. Max: {6:0.0}. Average: {7:0.0}. Standard deviation: {8:0.0}", numData.values.Count, dataPool.Count, m.Body, numData.nullcount, numData.errorcount, min, max, mean, stDev);
					}
				}
				else {  //Histogram-type analysis, ~10 most frequent
					int nullCount = data.Values.Where(x => x != "Not set").Count();
					Utils.Collections.Counter<string> DataTypes = new Utils.Collections.Counter<string>(data.Values.Where(x => x != "Not set"));
					results = string.Format(CultureInfo.InvariantCulture, "Quantitative analysis of {0} users for '{1}' complete. {2} users had no value set. {3} unique categories found, displaying a maximum of 10:\n{4}", dataPool.Count, m.Body, nullCount, DataTypes.Count, DataTypes.ToString(10));
				}
				//foreach (User u in dataPool) { u.Dispose(); }
				m.Reply(results, IO.ReplyMode.AsOriginal);
			}

			internal struct DataScan {
				internal Dictionary<string, float> values;
				internal int nullcount;
				internal int errorcount;
			}

			internal async Task<DataScan> ProcessNumericData(Dictionary<string, string> data) {
				DataScan result = new DataScan() {
					values = new Dictionary<string, float>(),
					errorcount = 0,
					nullcount = 0
				};
				await Task.Run(() => {
					foreach (var item in data) {
						System.Text.RegularExpressions.Match match = Utils.RegularExpressions.Numbers.Match(item.Value);
						if (match.Success) {
							if (float.Parse(match.Value) == 0) { result.nullcount++; continue; }
							result.values.Add(item.Key, float.Parse(match.Groups[0].Value));
						}
						else {
							Core.ErrorLog.Log("Error during Rexex Match of Scan() data for user " + item.Key, true);
							result.errorcount++;
						}
					}
				});
				return result;
			}
		}// plugin scan
		sealed class Shutdown : CogitoPlugin {
			public override string Name { get { return "Shutdown"; } }
			public override string Description { get { return "Commands an orderly remote shutdown of the application"; } }
			public override string Trigger { get { return Config.AppSettings.TriggerPrefix + "shutdown"; } }
			public override AccessLevel AccessLevel { get { return AccessLevel.ChannelOps; } }
			public override AccessPath AccessPath { get { return AccessPath.All; } }

			public override void MessageLoopMethod(Message m) { return; }
			public override void ShutdownMethod() { return; }
			public override void SetupMethod() { return; }

			public override IHost Host { get { return Core.pluginHost; } }

			public override void PluginMethod(Message m) { Core._quitEvent.Set(); }
		}//plugin Shutdown
		sealed class Whitelist : CogitoPlugin {
			public override string Name { get { return "Whitelist"; } }
			public override string Description { get { return "Bypases the Age Verification subroutine for approved ageless/unparseable users."; } }
			public override string Trigger { get { return (Config.AppSettings.TriggerPrefix + "whitelist"); } }
			public override AccessLevel AccessLevel { get { return AccessLevel.ChannelOps; } }
			public override AccessPath AccessPath { get { return AccessPath.All; } }

			public override void MessageLoopMethod(Message m) { return; }
			public override void ShutdownMethod() { return; }
			public override void SetupMethod() { return; }

			public override IHost Host { get { return Core.pluginHost; } }

			public override void PluginMethod(Message m) {
				if (m.sourceChannel == null) { 
					m.Reply("Error: No source channel attached to request. Unable to comply. Reporting error to support team..."); 
					Core.ErrorLog.Log("No channel attached to whitelist request: " + m.Body); 
					return;
				}
				Core.SystemLog.Log(m.Args.ToString(), true);
				if (m.Args.Length == 0) {
					m.Reply("Current whitelist for Channel '" + m.sourceChannel.Name + "':\n" + string.Join(", ", m.sourceChannel.Whitelist).TrimEnd(','));
					return;
				}
				string TargetUser = m.Body;
                if (!m.sourceChannel.Users.Select(x => x.Name).Contains(TargetUser)) { 
					m.Reply("'" + TargetUser + "' is not currently a user of channel '" + m.sourceChannel.Name + "'. For security purposes, and to avoid misspelling issues, only users in the channel can be added to the whitelist.");
					return; 
				}
				else {
					m.sourceChannel.Whitelist.Add(TargetUser);
					m.sourceChannel.ChannelModLog.Log(string.Format("Adding user '{0}' to channel whitelist by order of '{1}' ({2})", TargetUser, m.sourceUser.Name, m.AccessLevel));
					m.Reply("User '" + TargetUser + "' successfully added to whitelist for channel '" + m.sourceChannel.Name + "'.");
					Core.SaveAllSettingsBinary();
				}
			}
		}// plugin Whitelist
		sealed class YouTube : CogitoPlugin {
			public override string Name { get { return "YouTube Parser"; } }
			public override string Description { get { return "Passively waits for YouTube links, and, if one is found in a method, grabs video data and displays it."; } }
			public override string Trigger { get { return (Config.AppSettings.TriggerPrefix + "YTDUMMYTRIGGER"); } }
			public override AccessLevel AccessLevel { get { return AccessLevel.RootOnly; } }
			public override AccessPath AccessPath { get { return AccessPath.PMOnly; } }

			private System.Text.RegularExpressions.Regex YouTubeRegEx = new System.Text.RegularExpressions.Regex(@"(youtube.com.*v=|youtu.be/)(\S{11})");
			private char FullStar = '\u2605';
			//private char EmptyStar = '\u2606';

			public override async void MessageLoopMethod(Message m) {
				System.Text.RegularExpressions.Match YTMatch = YouTubeRegEx.Match(m.Body);
				if (YTMatch.Success) {
					string url = "http://youtube.com/get_video_info?video_id=" + YTMatch.Groups[2].Value; 
					string data;
					using (System.Net.WebClient w = new System.Net.WebClient()) { data = await w.DownloadStringTaskAsync(url); }
					Dictionary<string, string> YTData = new Dictionary<string, string>();
					YTData = data.Split('&').Select(n => n.Split('=')).ToDictionary(n => System.Web.HttpUtility.UrlDecode(n[0]), n => System.Web.HttpUtility.UrlDecode(n[1]));
					try {
						TimeSpan ytDuration = TimeSpan.FromSeconds(double.Parse(YTData["length_seconds"]));
						int Rating = (int)Math.Round(double.Parse(YTData["avg_rating"], Core.nfi), 2);
						m.Reply(string.Format("[color=Red]You[/color][color=white]Tube[/color] - [color=green]{0} ({1}) :: {2} :: {3}[/color]", YTData["title"], YTData["author"], ytDuration, new string(FullStar, Rating)));
					}
					catch { }
                }
			}

			public override void ShutdownMethod() { }
			public override void SetupMethod() { 
				FListProcessor.ChatMessage += MessageLoopMethod;
				Core.SystemLog.Log("Registered Loop Method of Plugin '" + Name + "...");
			}
			public override IHost Host { get { return Core.pluginHost; } }
			public override void PluginMethod(Message m) { }

		}// plugin YouTube
		sealed class DEFAULT : CogitoPlugin {
			public override string Name { get { return ""; } }
			public override string Description { get { return ""; } }
			public override string Trigger { get { return (Config.AppSettings.TriggerPrefix + "TRIGGER"); } }
			public override AccessLevel AccessLevel { get { return AccessLevel.Everyone; } }
			public override AccessPath AccessPath { get { return AccessPath.All; } }

			public override void MessageLoopMethod(Message m) { }
			public override void ShutdownMethod() { }
			public override void SetupMethod() { }

			public override IHost Host { get { return Core.pluginHost; } }

			public override void PluginMethod(Message m) { }

		}// plugin DEFAULT
	}
}
