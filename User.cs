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
		public readonly string _Name = null;

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
			_Name = nName.ToLowerInvariant();
			Age = -1;
			Core.allGlobalUsers.Add(this);
		}

		public User(string nName, int nAge) : this(nName) { Age = nAge; }

		public override string ToString() { return Name; }

		public void Message(string message, string Opcode = "PRI") {
			IO.Message m = new IO.Message();
			m.sourceUser = this;
			m.Body = message;
			m.Send();
		}

		public override int GetHashCode() { return _Name.GetHashCode(); }

		public bool Equals(User user) {
			if (_Name == user._Name) { return true; }
			else { return false; }
		}

		public override bool Equals(object obj) {
			if (obj == null) { return false; }
			User o = obj as User;
			if (_Name == o._Name) { return true; }
			else { return false; }
		}

		public int CompareTo(object obj) {
			if (obj == null) { return 1; }
			User o = obj as User;
			if (o != null) { return _Name.CompareTo(o._Name); }
			else { throw new ArgumentException("Object cannot be made into User. Cannot CompareTo()."); }
		}

		internal void Kick(Channel c) {
			if (Core.OwnUser.IsChannelOp(c)) {
				IO.SystemCommand s = new IO.SystemCommand();
				s.OpCode = "CKU";
				s.Data["character"] = Name;
				s.Data["channel"] = c.Key;
				s.Send();
			}
		}

		internal void Ban(Channel c) {
			if (Core.OwnUser.IsChannelOp(c)) {
				IO.SystemCommand s = new IO.SystemCommand();
				s.OpCode = "CBU";
				s.Data["character"] = Name;
				s.Data["channel"] = c.Key;
				s.Send();
			}
		}

		internal void Timeout(Channel c, int duration) {
			if (Core.OwnUser.IsChannelOp(c)) {
				IO.SystemCommand s = new IO.SystemCommand();
				s.OpCode = "CTU";
				s.Data["character"] = Name;
				s.Data["channel"] = c.Key;
				s.Data["length"] = duration;
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

		internal void Log(string s, bool suppressPrint = false) { userLog.Log(s, suppressPrint); }
		internal void Log(IO.Message m, bool suppressPrint = false) { userLog.Log(m, suppressPrint); }

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
			FormData.Add("account", WebUtility.HtmlEncode(Core.XMLConfig["account"]));
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

			//lock (UserLock) {
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
				hasData = true;
			//}
		}

		internal void MessageReceived(IO.Message m) {
			if (userLog == null) { 
				userLog = new IO.Logging.LogFile(Name, subdirectory: Name, timestamped: true);
				Core.ActiveUserLogs.Add(userLog);
			}
			Log(m);
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
