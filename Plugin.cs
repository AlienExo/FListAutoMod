using System;
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

		//CONSIDER DELEGATES
		/* 
		 * Plugin interface:
		 * (Line)processLine( Line lineToProcess) {}
		 * Object<Host> delegate
		 *
		 * Host interface:
		 * (AccessList) getAccessList
		 * (void)logAnEvent(LogEvent log)
		 *
		 */

		internal static void loadPlugins() {
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
			AddInternalPlugin<Shutdown>();
			AddInternalPlugin<Minage>();
			AddInternalPlugin<Scan>();
			AddInternalPlugin<RainbowText>();
			AddInternalPlugin<Horoscopes>();
			AddInternalPlugin<Whitelist>();
			AddInternalPlugin<Remote>();
			AddInternalPlugin<Admin>();
			AddInternalPlugin<ListChannels>();
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
					if (m.args.Contains("-r")) {
						int optIndex = Array.IndexOf(m.args, "-r");
						if (optIndex + 1 <= m.args.Length) {
							UnderageReponse newResponse = new UnderageReponse();
							if (Enum.TryParse<UnderageReponse>(m.args[optIndex + 1], out newResponse)) { m.sourceChannel.underageResponse = newResponse; }
						}
					}
					if (m.args.Contains("-a")) { 
						short newMinAge, oldMinAge;
						oldMinAge = m.sourceChannel.minAge;
						int optIndex = Array.IndexOf(m.args, "-a");
						if (short.TryParse(m.args[optIndex+1], out newMinAge)) {
							m.sourceChannel.minAge = newMinAge;
							string changeMsg = string.Format("Minimum age for '{0}' changed from '{1}' to '{2}' by '{3}' ({4}). Enforcement mode: {5}", m.sourceChannel, oldMinAge, newMinAge, m.sourceUser, m.AccessLevel, m.sourceChannel.underageResponse);
							m.sourceChannel.ChannelModLog.Log(changeMsg);
						}
						else { m.Reply("Cannot parse '" + m.args[optIndex+1] + "' as number. Expected format: .minage <newMinAge> (-r Kick|Ignore|Warn|Alert)"); }
					}
					m.Reply(string.Format("Settings for channel '{0}': Control system {1}. Minimum age: {2}. Enforcement mode: {3}.", m.sourceChannel.Name, m.sourceChannel.minAge > 0 ? "enabled" : "disabled", m.sourceChannel.minAge, m.sourceChannel.underageResponse), IO.ReplyMode.AsOriginal);
				}
				try { Core.SaveAllSettingsBinary(); } //Core.SaveAllSettingsXML();
				catch (Exception e) { Core.ErrorLog.Log(string.Format("Failed to save user data after changing settings for channel {1}, args {2} - {3}", m.sourceChannel, m.args, e.StackTrace)); }
			}
		}// plugin minage

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
				bool modeAnonymous = m.args.Contains("-a") ? true : false; //Are max and min named? Requires modeStatistic
				bool modeStatistic = m.args.Contains("-s") ? true : false;
				bool modeGreater = m.args.Contains("-gt") ? true : false;
				bool modeLesser = m.args.Contains("-lt") ? true : false;
				m.args = m.args.Where(n => n != "-a").ToArray();
				m.args = m.args.Where(n => n != "-s").ToArray();
				m.args = m.args.Where(n => n != "-gt").ToArray();
				m.args = m.args.Where(n => n != "-lt").ToArray();

				float cutoffValue = 0f;

				if (modeGreater && modeLesser) { m.Reply("Cannot scan for both GreaterThan and LesserThan. Decide."); return; }
				if (modeAnonymous && !modeStatistic) { m.Reply("Deactivating mode anonymous is only supported with mode statistic."); return; }
				if ((modeLesser || modeGreater) && !modeStatistic) { modeStatistic = true; }
				if (modeLesser || modeGreater) { cutoffValue = float.Parse(m.args[0]); m.args = m.args.Skip(1).ToArray(); }

				List<User> dataPool = m.sourceChannel.Users.ToList();
				Dictionary<string, string> data = new Dictionary<string, string>(dataPool.Count);
				string results;

				Task DataScan = Task.WhenAll(dataPool.Select(n => n.GetBasicProfileInfo()));
				await DataScan;

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
				foreach (User u in dataPool) { u.Dispose(); }
				m.Reply(results, IO.ReplyMode.AsOriginal);
			}

			internal struct DataScan {
				internal Dictionary<string, float> values;
				internal int nullcount;
				internal int errorcount;
			}

			internal async Task<DataScan> ProcessNumericData(Dictionary<string, string> data) {
				DataScan result = new DataScan();
				result.errorcount = 0;
				result.nullcount = 0;
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

		sealed class Horoscopes : CogitoPlugin {
			public override string Name { get { return "Celestial Guidance Extrapolation"; } }
			public override string Description { get { return "Blessed Child, heed the wisdom of the stars. You idiot."; } }
			public override string Trigger { get { return (Config.AppSettings.TriggerPrefix + "hs"); } }
			public override AccessLevel AccessLevel { get { return AccessLevel.Everyone; } }
			public override AccessPath AccessPath { get { return AccessPath.All; } }

			public override void MessageLoopMethod(Message m) { return; }
			public override void ShutdownMethod() { return; }
			public override void SetupMethod() { return; }

			public override IHost Host { get { return Core.pluginHost; } }

			string[] months = { "january", "february", "march", "april", "may", "june", "july", "august", "september", "october", "november", "december" };
			string[] days = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
			string[] elements = { "Hydrogen", "Helium", "Lithium", "Beryllium", "Boron", "Carbon", "Nitrogen", "Oxygen", "Fluorine", "Neon", "Sodium", "Magnesium", "Aluminum", "Silicon", "Phosphorus", "Sulfur", "Chlorine", "Argon", "Potassium", "Calcium", "Scandium", "Titanium", "Vanadium", "Chromium", "Manganese", "Iron", "Cobalt", "Nickel", "Copper", "Zinc", "Gallium", "Germanium", "Arsenic", "Selenium", "Bromine", "Krypton", "Rubidium", "Strontium", "Yttrium", "Zirconium", "Niobium", "Molybdenum", "Technetium", "Ruthenium", "Rhodium", "Palladium", "Silver", "Cadmium", "Indium", "Tin", "Antimony", "Tellurium", "Iodine", "Xenon", "Cesium", "Barium", "Hafnium", "Tantalum", "Tungsten", "Rhenium", "Osmium", "Iridium", "Platinum", "Gold", "Mercury", "Thallium", "Lead", "Bismuth", "Polonium", "Astatine", "Radon", "Francium", "Radium", "Unnilquadium", "Unnilpentium", "Unnilhexium", "Unnilseptium", "Unniloctium", "Unnilennium", "Ununnilium", "Unununium", "Ununbium", "Lanthanum", "Cerium", "Praseodymium", "Neodymium", "Promethium", "Samarium", "Europium", "Gadolinium", "Terbium", "Dysprosium", "Holmium", "Erbium", "Thulium", "Ytterbium", "Lutetium", "Actinium", "Thorium", "Protactinium", "Uranium", "Neptunium", "Plutonium", "Americium", "Curium", "Berkelium", "Californium", "Einsteinium", "Fermium", "Mendelevium", "Nobelium", "Lawrencium" };
			
			Dictionary<int, int> mon_len = new Dictionary<int, int> { { 1, 0 }, { 2, 32 }, { 3, 60 }, { 4, 91 }, { 5, 121 }, { 6, 152 }, { 7, 182 }, { 8, 213 }, { 9, 243 }, { 10, 274 }, { 11, 305 }, { 12, 335 } };
			
			Dictionary<int, string> signs = new Dictionary<int, string> { { 20, "Capricorn" }, { 50, "Aquarius" }, { 79, "Pisces" }, { 110, "Aries" }, { 141, "Taurus" }, { 172, "Gemini" }, { 202, "Cancer" }, { 234, "Leo" }, { 266, "Virgo" }, { 296, "Libra" }, { 326, "Scorpio" }, { 356, "Sagittarius" }, { 366, "Capricorn" } };
			
			List<string> horoscopes = File.ReadAllText(Config.AppSettings.DataPath + "horoscopes.txt").Split('\n').ToList();

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
				if (m.sourceChannel != null) { horoscope = horoscope.Replace("{NAME}", Utils.Math.RandomChoice(m.sourceChannel.Users).Name); }
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
				bool modeRaw = m.args.Contains("-r") ? true : false;
				m.args = m.args.Where(n => n != "-r").ToArray();
				bool modeSay = m.args.Contains("-s") ? true : false;
				m.args = m.args.Where(n => n != "-s").ToArray();
				bool modeAct = m.args.Contains("-a") ? true : false;
				m.args = m.args.Where(n => n != "-a").ToArray();
				if ((modeRaw ? 1 : 0) + (modeSay ? 1 : 0) + (modeAct ? 1 : 0) > 1) { m.Reply("One mode only.", IO.ReplyMode.ForcePM); return; }
				if (modeRaw) { Core.websocket.Send(m.Body); return; }
				if (modeAct) { m.Reply("/me " + m.Body, IO.ReplyMode.ForceChannel); }
				else { m.Reply(m.Body, IO.ReplyMode.ForceChannel); }
			}

		}// plugin DEFAULT

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
				if (m.args.Length < 1) {
					m.Reply("Current whitelist for Channel '" + m.sourceChannel.Name + "':\n" + string.Join(", ", m.sourceChannel.Whitelist).TrimEnd(','));
					return;
				}
				string TargetUser = m.Body;
                if (!m.sourceChannel.Users.Select(x => x.Name).Contains(TargetUser)) { 
					m.Reply("'" + TargetUser + " is not currently a user of channel '" + m.sourceChannel.Name + "'. For security purposes, and to avoid misspelling issues, only users in the channel can be added to the whitelist.");
					return; 
				}
				else {
					m.sourceChannel.Whitelist.Add(TargetUser);
					m.sourceChannel.ChannelModLog.Log(string.Format("Adding user '{0}' to channel whitelist by order of '{1}' ({2})", TargetUser, m.sourceUser.Name, m.AccessLevel));
					m.Reply("User '" + TargetUser + "' successfully added to whitelist for channel '" + m.sourceChannel.Name + "'.");
				}
			}
		}// plugin DEFAULT

		sealed class ListChannels : CogitoPlugin { //TODO
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
				string reply = "Currently joined channels, ordered by their Redirect Key, are:\n";
				for (int i = 0; i < Core.joinedChannels.Count; i++) { reply += string.Format("\t{0}: '{1}' [{2}]\n", i, Core.joinedChannels[i].Name, Core.joinedChannels[i].Key); }
				m.Reply(reply, IO.ReplyMode.ForcePM);
			}

		}// plugin DEFAULT

		sealed class Admin : CogitoPlugin { //TODO
			public override string Name { get { return "Adminstration Tools"; } }
			public override string Description { get { return "Allows execution of channel Op commands via Cogito"; } }
			public override string Trigger { get { return (Config.AppSettings.TriggerPrefix + "op"); } }
			public override AccessLevel AccessLevel { get { return AccessLevel.Everyone; } }
			public override AccessPath AccessPath { get { return AccessPath.All; } }

			public override void MessageLoopMethod(Message m) { return; }
			public override void ShutdownMethod() { return; }
			public override void SetupMethod() { return; }

			public override IHost Host { get { return Core.pluginHost; } }

			public override void PluginMethod(Message m) {
				if (m.sourceChannel == null) { m.Reply("No source channel attached to command. Either use command from within a channel, or use the redirect operator '" + Config.AppSettings.RedirectOperator + "' plus a channel ID number starting at 0 (obtained using .ls) to select the correct channel."); return; }
				bool modeKick = m.args.Contains("-k") ? true : false;
				m.args = m.args.Where(n => n != "-k").ToArray();
				bool modeBan = m.args.Contains("-b") ? true : false;
				m.args = m.args.Where(n => n != "-b").ToArray();
				bool modeTimeout = m.args.Contains("-t") ? true : false;
				m.args = m.args.Where(n => n != "-t").ToArray();
				bool modeSass = m.args.Contains("-s") ? true : false;
				m.args = m.args.Where(n => n != "-s").ToArray();
				//bool modeOp = m.args.Contains("-op") ? true : false;
				//m.args = m.args.Where(n => n != "-op").ToArray();
				//bool modeDeop = m.args.Contains("-deop") ? true : false;
				//m.args = m.args.Where(n => n != "-deop").ToArray();
				int timeoutLength = -1;
				if (m.args.Length < 1) { m.Reply("Not enough arguments supplied. Need at least mode and target name."); return; }
				if (modeTimeout && int.TryParse(m.args[0], out timeoutLength)) { m.args = m.args.Skip(1).ToArray(); } 
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

		}// plugin DEFAULT

		sealed class DEFAULT : CogitoPlugin {
			public override string Name { get { return ""; } }
			public override string Description { get { return ""; } }
			public override string Trigger { get { return (Config.AppSettings.TriggerPrefix + "TRIGGER"); } }
			public override AccessLevel AccessLevel { get { return AccessLevel.Everyone; } }
			public override AccessPath AccessPath { get { return AccessPath.All; } }

			public override void MessageLoopMethod(Message m) { return; }
			public override void ShutdownMethod() { return; }
			public override void SetupMethod() { return; }

			public override IHost Host { get { return Core.pluginHost; } }

			public override void PluginMethod(Message m) { }

		}// plugin DEFAULT
	}
}
