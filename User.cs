using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using HtmlAgilityPack;
using Newtonsoft.Json;

namespace CogitoMini {
	enum Status : int { online = 0, crown, looking, idle, busy, dnd, away, offline }
	/// <summary>User (synonymous with Character)</summary>
	[Serializable]
	public class User : IComparable, IDisposable {
		[NonSerialized]
		private static int Count;
		[NonSerialized]
		private readonly int UID = ++Count;
		[NonSerialized]
		internal object UserLock = new object();
		[NonSerialized]
		internal IO.Logging.LogFile userLog = null;
		[NonSerialized]
		private bool disposed = false;
		[NonSerialized]
		internal bool isMod = false;

		internal Dictionary<string, string> data = new Dictionary<string, string>();

		[OnDeserialized]
		private void SetValuesOnDeserialized(StreamingContext context) { UserLock = new object(); }

		//internal bool hasSettings = false;
		internal bool hasData = false;
		internal bool Ignore = false;

		/// <summary> xXxSEPHIROTHxXx </summary>
		public readonly string Name = null;

		/// <summary> 2 shota 4 u </summary>
		public int Age {
			get {
				if (data.ContainsKey("age")) {
					try { return int.Parse(data["age"]); }
					catch { return 0; }
				}
				else { return -1; }
			}
			set { data["age"] = value.ToString(); }
		}

		internal string Gender {
			get { if (data.ContainsKey("gender")) { return data["gender"]; } else { return "Not set"; } }
			set { data["gender"] = value; }
		}

		internal string Species {
			get { if (data.ContainsKey("species")) { return data["species"]; } else { return "Not set"; } }
			set { data["species"] = value; }
		}

		/// <summary> Sexual orientation; can't be made an enum due to hyphens not working and that fucking up parsing, iirc</summary>
		internal string Orientation {
			get { if (data.ContainsKey("orientation")) { return data["orientation"]; } else { return "Not set"; } }
			set { data["orientation"] = value; }
		}

		internal string Height {
			get { if (data.ContainsKey("height")) { return data["height"]; } else { return "Not set"; } }
			set { data["height"] = value; }
		}

		internal string Position {
			get { if (data.ContainsKey("position")) { return data["position"]; } else { return "Not set"; } }
			set { data["position"] = value; }
		}

		internal string TryGetData(string key) { if (data.ContainsKey(key.ToLowerInvariant())) { return data[key.ToLowerInvariant()]; } else { return "Not set"; } }

		//internal string FurryPreference;
		//Utils.Math.Measurement<float> Height;

		internal Status Status = Status.online;
		internal string StatusMessage;

		/// <summary> Stores the DateTime on which the profile was last scraped, allowing the program to self-update every... what, week?</summary>
		internal DateTime dataTakenOn = new DateTime(1, 1, 1);

		public User(string nName) {
			Name = nName.Trim('\t', '\r', '\n', ' ');
			this.Age = -1;
			Core.allGlobalUsers.Add(this);
		}

		public User(string nName, int nAge) : this(nName) { Age = nAge; }

		public override string ToString() { return Name; }

		public override bool Equals(object obj) {
			if (obj == null) { return false; }
			User o = obj as User;
			if (Name == o.Name) { return true; }
			else { return false; }
		}

		public void Message(string message, string Opcode = "PRI") {
			IO.Message m = new IO.Message();
			m.sourceUser = this;
			m.Data["message"] = message;
			m.Send();
		}

		public override int GetHashCode() { return Name.GetHashCode(); }

		public bool Equals(User user) {
			if (Name == user.Name) { return true; }
			else { return false; }
		}

		public int CompareTo(object obj) {
			if (obj == null) { return 1; }
			User o = obj as User;
			if (o != null) { return Name.CompareTo(o.Name); }
			else { throw new ArgumentException("Object cannot be made into User. Cannot CompareTo()."); }
		}

		internal void Kick(Channel c) {
			if (!Core.OwnUser.IsChannelOp(c)) {
				IO.SystemCommand s = new IO.SystemCommand();
				s.OpCode = "CKU";
				s.Data["character"] = this.Name;
				s.Data["channel"] = c.Key;
				s.Send();
			}
		}

		internal bool IsGlobalOp() {
			if (Core.globalOps.Contains(this)) { return true; }
			return false;
		}

		internal bool IsChannelOp(Channel c) {
			if (c.Mods.Contains(this)) { return true; }
			return false;
		}

		/// <summary>
		/// Gets basic character info (Name, Age, Gender, Height, Orientation) but no Kinks or Art.
		/// </summary>
		public async Task GetBasicProfileInfo(bool useAPIv2 = false) { //TODO This function doesn't run async. Why?
			string targetAPI = useAPIv2 ? Config.URLConstants.V2.CharacterInfo : Config.URLConstants.V1.CharacterInfo;
			Dictionary<string, string> ProfileData = new Dictionary<string, string>();
			FListAPI.CharacterResponseBase DataDigest = new FListAPI.CharacterResponseBase();
			IEnumerable<FListAPI.Item> Members = null;

			System.Collections.Specialized.NameValueCollection FormData = new System.Collections.Specialized.NameValueCollection();
			FormData.Add("name", WebUtility.HtmlEncode(Name));
			if ((DateTime.Now - Account.LoginKey.ticketTaken) >= Config.AppSettings.ticketLifetime) { Account.getTicket(); }
			FormData.Add("ticket", WebUtility.HtmlEncode(Account.LoginKey.ticket));
			FormData.Add("account", WebUtility.HtmlEncode(System.Configuration.ConfigurationManager.AppSettings.Get("account")));
			if (useAPIv2) { FormData.Add("infotags", "1"); }
			WebHeaderCollection FormHeaders = new WebHeaderCollection();
			FormHeaders.Add(HttpRequestHeader.ContentType, "application/x-www-form-urlencoded");

			WebClient w = new WebClient();
			w.Headers = FormHeaders;
			if (useAPIv2) { DataDigest = JsonConvert.DeserializeObject<FListAPI.CharacterDataAPIv2>(System.Text.Encoding.UTF8.GetString(await w.UploadValuesTaskAsync(targetAPI, "POST", FormData))); }
			else { DataDigest = JsonConvert.DeserializeObject<FListAPI.CharacterDataAPIv1>(System.Text.Encoding.UTF8.GetString(await w.UploadValuesTaskAsync(targetAPI, "POST", FormData))); }
			
			if (DataDigest.error.Length > 0) { //gotta go HTML
				Console.WriteLine("API Error: " + DataDigest.error);
				FormHeaders.Add(HttpRequestHeader.Cookie, "warning=1");
				FormHeaders.Remove(HttpRequestHeader.ContentType);
				string profile = await w.DownloadStringTaskAsync(new Uri(Config.URLConstants.ProfileRoot + WebUtility.HtmlEncode(Name))).ConfigureAwait(false);
				HtmlDocument profileDoc = new HtmlDocument();
				profileDoc.LoadHtml(profile);
				//profile = WebUtility.HtmlDecode(profile);
				//if (profile.IndexOf("<div class='itgroup'>") > 0) { profile = profile.Substring(profile.IndexOf("<div class='itgroup'>")); }
				string PTag = profileDoc.DocumentNode.Descendants("div").Where(d => d.Attributes.Contains("class") && d.Attributes["class"].Value == "itgroup").First().ToString();
				MatchCollection dataTags = new Regex(@"<span class=""taglabel"">(?<key>.*?):</span>(?<value>.*?)<br/>").Matches(PTag);
				foreach (Match profileitem in dataTags) { if (profileitem.Groups["key"].Success && profileitem.Groups["value"].Success) { ProfileData.Add(profileitem.Groups["key"].Value, profileitem.Groups["value"].Value); } }
			}
			w.Dispose();
			lock (UserLock) {
				Age = 0;
				if (useAPIv2) { //nice, direct parsing that isn't online yet because fuck you
					FListAPI.Infotags CharInfo = ((FListAPI.CharacterDataAPIv2)DataDigest).infotags;
					data = CharInfo.ToDictionary();
					Age = CharInfo.Age;
					Gender = CharInfo.Gender;
					Orientation = CharInfo.Orientation;
					//Height = Utils.Math.parseMeasurementFromDescription<float>(CharInfo.Height, Utils.Math.MeasurementUnit.Length);
					Height = CharInfo.Height;
					Position = CharInfo.Position;
					Species = CharInfo.Species;
				}
				else { //dirty ol' parsing
					if (DataDigest.error.Length == 0) { //It's an API V1 parse and ProfileData is still empty
						Members = ((FListAPI.CharacterDataAPIv1)DataDigest).info.SelectMany(n => n.Value.items);
						ProfileData = Members.Select(x => new { x.name, x.value }).ToDictionary(y => y.name.ToLowerInvariant(), y => y.value);
						data = ProfileData;
					}
					else { ProfileData.Select(n => { data[n.Key] = n.Value; return n; }); }
					if (ProfileData.ContainsKey("age")) {
						try { Age = int.Parse(Utils.RegularExpressions.AgeSearch.Match(ProfileData["age"]).Value); }    //remove all non-numerics from age string
						catch (Exception) { }
					}
				}
				dataTakenOn = DateTime.Now; //sets the flag
			}
		}

		internal void MessageReceived(IO.Message m) {
			if (userLog == null) {
				userLog = new IO.Logging.LogFile(Name, subdirectory: Name, timestamped: true);
				Core.ActiveUserLogs.Add(userLog);
			}
			userLog.Log(m);
		}

		internal async void CheckAge(Channel c) {
			if (c.Whitelist.Contains(Name)) { return; }
			if ((DateTime.Now - dataTakenOn) >= Config.AppSettings.userProfileRefreshPeriod) { await GetBasicProfileInfo(); }
			//await GetBasicProfileInfo();
			//Console.WriteLine(string.Format("User {0} has entered channel {1}. User age is {2}, channel minage is {3}", Name, c.Name, Age, c.minAge));
			if (Age <= 0 && c.underageResponse != UnderageReponse.Ignore) {
				int QueuePosition = c.WhitelistQueue.Contains(Name) ? c.WhitelistQueue.IndexOf(Name) : c.WhitelistQueue.Count;
				int ChannelInteger = Core.joinedChannels.Contains(c) ? Core.joinedChannels.IndexOf(c) : -1;
				if (ChannelInteger == -1) { throw new InvalidOperationException("Target Channel " + c.Name + " is not registered in Core.joinedChannels and should not be being monitored..."); }
				c.TryAlertMod(Name, string.Format("Cannot parse age of User '{0}' joining channel '{1}' (minimum age set to {2}). Please verify.\n In case of false positive, please respond with '.whitelist {3} => {4}'. [BETA]", Name, c.Name, c.minAge, QueuePosition, ChannelInteger));
				if (Age == -1) { Core.ErrorLog.Log("Age check failed for user " + Name + "; check subroutine."); return; }
				return;
			}
			if (Age < c.minAge) {
				switch (c.underageResponse) {
					default:
					case UnderageReponse.Alert:
						c.TryAlertMod(Name, string.Format("[b]Warning![/b] User '{0}' is below minimum age ({1}) for channel '{2}'. Auto-Kick is disabled. Please proceed manually.", Name, c.minAge, c.Name));
						break;

					case UnderageReponse.Ignore:
						break;

					case UnderageReponse.Kick:
						Message(string.Format("This is an automated message. You, user '{0}', are below the minimum age ({1}) for channel '{2}' and will be removed. You are welcome to join with an of-age character. Have a nice day.", Name, c.minAge, c.Name));
						c.Kick(this);
						break;

					case UnderageReponse.Warn:
						c.Message(string.Format("This is an automated message. User '{0}' is below the minimum age ({1}) for channel '{2}'. Please leave or return with an of-age character.", Name, c.minAge, c.Name));
						//c.TryAlertMod(Name, string.Format("[b]Warning![/b] User '{0}' is below minimum age ({1}) for channel '{2}'. User has been warned.", Name, c.minAge, c.Name));
						break;
				}
			}
		}

		public virtual void Dispose() {
			if (!disposed) {
				if (userLog != null) {
					userLog.Dispose();
					Core.ActiveUserLogs.Remove(userLog);
				}
			}
			disposed = true;
		}
	}
}
