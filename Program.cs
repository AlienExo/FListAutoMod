using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

using Newtonsoft.Json;
using WebSocket4Net;

using CogitoMini.IO;


#region changelog
/* Changelog
 * >Insert whitelist
 *
 *
 *
 */
#endregion

/*	--- TODO  ---
 * 
 * [||||||||||] Connect to server 
 * [||||||||||] Join channel 
 * [||||||||||] Parse age of incoming users 
 * [||||||||||] Kick those below certain age 
 * [||||||||||] Alert Mod(s) for non-parseable ppl
 * [||||||||||] Ignore list for mods who don't like robots
 * [||||||||||] Queue for undeliverable mesasges plus timing since last mod left
 * [----------] Expand to gender
 * [----------] Expand to arbitrary profile marks
 * [||||||||||] Save and retain configuration outside of core code
 * [----------] Timeouts for banned words
 *
 * consider a delegate for messageScan-type plugins who then subscribe to it (+= on plugin load)

*/

namespace CogitoMini
{
	/// <summary> Collection of all values and variables needed to establish a connection to an fchat server </summary>
	public class LoginKey {
		/// <summary>Server-side account number</summary>
		public int account_id { get; set; }
		/// <summary>character set as default on the server</summary>
		public string default_character { get; set; }
		/// <summary>All characters on the account. Limited to 30 for normal users.</summary>
		public List<string> characters { get; set; }
		/// <summary>Login error message (if any)</summary>
		public string error { get; set; }
		/// <summary>Characters bookmarked</summary>
		public List<Dictionary<string, string>> bookmarks { get; set; }
		/// <summary>List of characters befriended, and whom by</summary>
		public List<Dictionary<string, string>> friends { get; set; }
		/// <summary>The API Ticket used to access the system</summary>
		public string ticket { get; set; }
		/// <summary> The DateTime when this ticket was obtained; used to avoid </summary>
		public DateTime ticketTaken = DateTime.UtcNow;
	}

	class Account {
		protected internal static LoginKey LoginKey = null;
		protected internal static List<string> bookmarks = new List<string>();

		protected internal static void getTicket() {
            using (var wb = new WebClient()) {
				var data = new NameValueCollection();
				data["account"] = Core.XMLConfig["account"];
				data["password"] = Core.XMLConfig["password"];
				data["no_characters"] = "true";
				data["no_friends"] = "true";
				data["no_bookmarks"] = "true";
				var byteTicket = wb.UploadValues(Config.URLConstants.V1.GetTicket, "POST", data);
				string t1 = System.Text.Encoding.ASCII.GetString(byteTicket);
				LoginKey = JsonConvert.DeserializeObject<LoginKey>(t1);
				LoginKey.ticketTaken = DateTime.UtcNow;
				Core.SystemLog.Log("Refreshed ticket - " + LoginKey.ticket, true);
			}
		}

		protected internal static void login(string _account, string _password) {
			getTicket();
			if (LoginKey.error.Length > 0) {
				Core.ErrorLog.Log("Logging in with account " + _account + " failed: " + LoginKey.error);
				Core.SystemLog.Log("Logging in with account " + _account + " failed: " + LoginKey.error);
				Console.WriteLine("Press enter to close...");
				Console.ReadLine();
				Environment.Exit(0);
			}
			else {
				Core.SystemLog.Log("Successfully logged in.");
				var logindata = new Dictionary<string, string>();
				logindata["method"] = "ticket";
				logindata["account"] = _account;
				logindata["character"] = Core.XMLConfig["character"];
				logindata["ticket"] = Account.LoginKey.ticket;
				logindata["cname"] = "COGITO";
				logindata["cversion"] = Config.AppSettings.Version;
				string openString = JsonConvert.SerializeObject(logindata);
				openString = "IDN " + openString;
				Core.SystemLog.Log("Logging in with character '" + Core.XMLConfig["character"] + "'.");
				Core.websocket.Send(openString);
			}
		}
	}

	[Serializable]
	public class ConcurrentSet<T> : IEnumerable<T>, ISet<T>, ICollection<T>
	{
		private readonly ConcurrentDictionary<T, byte> _dictionary = new ConcurrentDictionary<T, byte>();

		/// <summary> Returns an enumerator that iterates through the collection. </summary>
		/// <returns> A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the collection. </returns>
		public IEnumerator<T> GetEnumerator(){ return _dictionary.Keys.GetEnumerator(); }

		/// <summary> Returns an enumerator that iterates through a collection. </summary>
		/// <returns> An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection. </returns>
		IEnumerator IEnumerable.GetEnumerator(){ return GetEnumerator(); }

		/// <summary> Removes the first occurrence of a specific object from the <see cref="T:System.Collections.Generic.ICollection`1"/>. </summary>
		/// <returns> true if <paramref name="item"/> was successfully removed from the <see cref="T:System.Collections.Generic.ICollection`1"/>; otherwise, false. This method also returns false if <paramref name="item"/> is not found in the original <see cref="T:System.Collections.Generic.ICollection`1"/>. </returns>
		/// <param name="item">The object to remove from the <see cref="T:System.Collections.Generic.ICollection`1"/>.</param><exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.</exception>
		public bool Remove(T item){ return TryRemove(item); }

		/// <summary>Gets the number of elements in the set. </summary>
		public int Count{ get { return _dictionary.Count; } }
		
		/// <summary> Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only. </summary>
		/// <returns> true if the <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only; otherwise, false. </returns>
		public bool IsReadOnly { get { return false; } }

		/// <summary> Gets a value that indicates if the set is empty. </summary>
		public bool IsEmpty{ get { return _dictionary.IsEmpty; } }

		public ICollection<T> Values{ get { return _dictionary.Keys; } }

		/// <summary>Adds an item to the <see cref="T:System.Collections.Generic.ICollection`1"/>. </summary>
		/// <param name="item">The object to add to the <see cref="T:System.Collections.Generic.ICollection`1"/>.</param>
		/// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.</exception>
		void ICollection<T>.Add(T item){
			if (!Add(item))
				throw new ArgumentException("Item already exists in set.");
		}

		/// <summary> Modifies the current set so that it contains all elements that are present in both the current set and in the specified collection. </summary>
		/// <param name="other">The collection to compare to the current set.</param><exception cref="T:System.ArgumentNullException"><paramref name="other"/> is null.</exception>
		public void UnionWith(IEnumerable<T> other){
			foreach (var item in other)
				TryAdd(item);
		}

		/// <summary>Modifies the current set so that it contains only elements that are also in a specified collection.</summary>
		/// <param name="other">The collection to compare to the current set.</param><exception cref="T:System.ArgumentNullException"><paramref name="other"/> is null.</exception>
		public void IntersectWith(IEnumerable<T> other){
			var enumerable = other as IList<T> ?? other.ToArray();
			foreach (var item in this)
			{
				if (!enumerable.Contains(item))
					TryRemove(item);
			}
		}

		/// <summary>Removes all elements in the specified collection from the current set. </summary>
		/// <param name="other">The collection of items to remove from the set.</param><exception cref="T:System.ArgumentNullException"><paramref name="other"/> is null.</exception>
		public void ExceptWith(IEnumerable<T> other){
			foreach (var item in other)
				TryRemove(item);
		}

		/// <summary>Modifies the current set so that it contains only elements that are present either in the current set or in the specified collection, but not both.  </summary>
		/// <param name="other">The collection to compare to the current set.</param><exception cref="T:System.ArgumentNullException"><paramref name="other"/> is null.</exception>
		public void SymmetricExceptWith(IEnumerable<T> other){
			throw new NotImplementedException();
		}

		/// <summary>Determines whether a set is a subset of a specified collection. </summary>
		/// <returns> true if the current set is a subset of <paramref name="other"/>; otherwise, false.</returns>
		/// <param name="other">The collection to compare to the current set.</param><exception cref="T:System.ArgumentNullException"><paramref name="other"/> is null.</exception>
		public bool IsSubsetOf(IEnumerable<T> other){
			var enumerable = other as IList<T> ?? other.ToArray();
			return this.AsParallel().All(enumerable.Contains);
		}

		/// <summary>Determines whether the current set is a superset of a specified collection.</summary>
		/// <returns>true if the current set is a superset of <paramref name="other"/>; otherwise, false.</returns>
		/// <param name="other">The collection to compare to the current set.</param><exception cref="T:System.ArgumentNullException"><paramref name="other"/> is null.</exception>
		public bool IsSupersetOf(IEnumerable<T> other){
			return other.AsParallel().All(Contains);
		}

		/// <summary>Determines whether the current set is a correct superset of a specified collection.</summary>
		/// <returns> true if the <see cref="T:System.Collections.Generic.ISet`1"/> object is a correct superset of <paramref name="other"/>; otherwise, false.</returns>
		/// <param name="other">The collection to compare to the current set. </param><exception cref="T:System.ArgumentNullException"><paramref name="other"/> is null.</exception>
		public bool IsProperSupersetOf(IEnumerable<T> other){
			var enumerable = other as IList<T> ?? other.ToArray();
			return Count != enumerable.Count && IsSupersetOf(enumerable);
		}

		/// <summary>Determines whether the current set is a property (strict) subset of a specified collection.</summary>
		/// <returns>true if the current set is a correct subset of <paramref name="other"/>; otherwise, false.</returns>
		/// <param name="other">The collection to compare to the current set.</param><exception cref="T:System.ArgumentNullException"><paramref name="other"/> is null.</exception>
		public bool IsProperSubsetOf(IEnumerable<T> other){
			var enumerable = other as IList<T> ?? other.ToArray();
			return Count != enumerable.Count && IsSubsetOf(enumerable);
		}

		/// <summary>Determines whether the current set overlaps with the specified collection.</summary>
		/// <returns>true if the current set and <paramref name="other"/> share at least one common element; otherwise, false.</returns>
		/// <param name="other">The collection to compare to the current set.</param><exception cref="T:System.ArgumentNullException"><paramref name="other"/> is null.</exception>
		public bool Overlaps(IEnumerable<T> other){
			return other.AsParallel().Any(Contains);
		}

		/// <summary>Determines whether the current set and the specified collection contain the same elements.</summary>
		/// <returns>true if the current set is equal to <paramref name="other"/>; otherwise, false.</returns>
		/// <param name="other">The collection to compare to the current set.</param><exception cref="T:System.ArgumentNullException"><paramref name="other"/> is null.</exception>
		public bool SetEquals(IEnumerable<T> other){
			var enumerable = other as IList<T> ?? other.ToArray();
			return Count == enumerable.Count && enumerable.AsParallel().All(Contains);
		}

		/// <summary>Adds an element to the current set and returns a value to indicate if the element was successfully added. </summary>
		/// <returns>true if the element is added to the set; false if the element is already in the set.</returns>
		/// <param name="item">The element to add to the set.</param>
		public bool Add(T item){
			return TryAdd(item);
		}

		public void Clear(){
			_dictionary.Clear();
		}

		public bool Contains(T item){
			return _dictionary.ContainsKey(item);
		}

		/// <summary>Copies the elements of the <see cref="T:System.Collections.Generic.ICollection`1"/> to an <see cref="T:System.Array"/>, starting at a particular <see cref="T:System.Array"/> index.</summary>
		/// <param name="array">The one-dimensional <see cref="T:System.Array"/> that is the destination of the elements copied from <see cref="T:System.Collections.Generic.ICollection`1"/>. The <see cref="T:System.Array"/> must have zero-based indexing.</param><param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param><exception cref="T:System.ArgumentNullException"><paramref name="array"/> is null.</exception><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is less than 0.</exception><exception cref="T:System.ArgumentException"><paramref name="array"/> is multidimensional.-or-The number of elements in the source <see cref="T:System.Collections.Generic.ICollection`1"/> is greater than the available space from <paramref name="arrayIndex"/> to the end of the destination <paramref name="array"/>.-or-Type <paramref name="T"/> cannot be cast automatically to the type of the destination <paramref name="array"/>.</exception>
		public void CopyTo(T[] array, int arrayIndex){
			Values.CopyTo(array, arrayIndex);
		}

		public T[] ToArray(){ return _dictionary.Keys.ToArray(); }

		public bool TryAdd(T item){ return _dictionary.TryAdd(item, default(byte)); }

		public bool TryRemove(T item){ 
			byte donotcare;
			return _dictionary.TryRemove(item, out donotcare);
		}
	}

	internal class PluginHost : IHost {
		HashSet<User> IHost.GetChannelModList(Channel c) { return c.Mods; }
		HashSet<User> IHost.GetChannelUserList(Channel c) { return c.Users;  }
		//ConcurrentSet<User> IHost.GetGlobalUserList() { return Core.allGlobalUsers;  }
		void IHost.LogToErrorFile(string s) { Core.ErrorLog.Log(s);  }
		void IHost.LogToSystemFile(string s) { Core.SystemLog.Log(s); }
	}

	/// <summary>Websocket handling, server connection, threading, all that goodness</summary>
	internal static class Core{
		internal static bool heartbeat = true;

		internal static List<string> Ops = new List<string>();
		internal static WebSocket websocket = null;
		internal static HashSet<Logging.LogFile> ActiveUserLogs = new HashSet<Logging.LogFile>();
		internal static ConcurrentSet<User> allGlobalUsers = new ConcurrentSet<User>(); //volatile?
		internal static ConcurrentSet<Channel> channels = new ConcurrentSet<Channel>(); //volatile?
		internal static List<Channel> joinedChannels = new List<Channel>();
		internal static List<User> globalOps = new List<CogitoMini.User>();
		internal static Queue<SystemCommand> IncomingMessageQueue = new Queue<IO.SystemCommand>();
		internal static Queue<SystemCommand> OutgoingMessageQueue = new Queue<IO.SystemCommand>();

		internal static FListAPI.Mapping APIMapping = null;
				
		internal static void SendMessageFromQueue(object stateobject){ 
			if (OutgoingMessageQueue.Count > 0){
				try{
					string _message = OutgoingMessageQueue.Dequeue().ToServerString();
					RawData.Log(">> " + _message);
					websocket.Send(_message);
				}
				catch (Exception ex) { ErrorLog.Log(string.Format("Sending message failed:\n\t{0}\n\t{1}\t{2}", ex.Message, ex.InnerException, ex.StackTrace)); }
			}
		}
		internal static bool _sendForever = true;
				
		internal static Logging.LogFile SystemLog = new Logging.LogFile("SystemLog", timestamped: true);
		internal static Logging.LogFile ErrorLog = new Logging.LogFile("ErrorLog", timestamped: true);
		internal static Logging.LogFile ModLog = new Logging.LogFile("ModLog", timestamped: true);
		internal static Logging.LogFile RawData = new Logging.LogFile("RawData", timestamped: true);

		internal static User OwnUser = null;
		internal static FListProcessor Processor = new FListProcessor();
		internal static Timer EternalSender;
		internal static Timer LaplacesDemon;
		internal static DateTime LastPurge;
		internal static ManualResetEvent _quitEvent = new ManualResetEvent(false);

		internal static PluginHost pluginHost = new PluginHost();

		internal static System.Globalization.NumberFormatInfo nfi = new System.Globalization.NumberFormatInfo();
		internal static Dictionary<string, string> XMLConfig = new Dictionary<string, string>();

		/// <summary>The main entry point for the application.</summary>
		[STAThread]
        static void Main(){
			nfi.NumberDecimalSeparator = ".";
			AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
			Console.CancelKeyPress += (sender, eArgs) => { _quitEvent.Set(); eArgs.Cancel = true; };
			Console.WindowHeight = Console.LargestWindowHeight;
			Console.WindowWidth = (Console.LargestWindowWidth / 2);

			if (!File.Exists(Config.AppSettings.DataPath + "App.config")) { 
				ErrorLog.Log("No App.config file in directory /data/! Cannot retrieve server and user settings. Shutting down...");
				Console.ReadKey();
				Environment.Exit(1);
			}

			ExeConfigurationFileMap configMap = new ExeConfigurationFileMap();
			configMap.ExeConfigFilename = Config.AppSettings.DataPath + "App.config";
			Configuration _XMLConfig = ConfigurationManager.OpenMappedExeConfiguration(configMap, ConfigurationUserLevel.None);
			foreach (KeyValueConfigurationElement ce in _XMLConfig.AppSettings.Settings) { XMLConfig.Add(ce.Key, ce.Value); }

			SystemLog.Log("Start up: CogitoMini v." + Config.AppSettings.Version);
			SystemLog.Log("Loading Plugins...");
			try{ Plugins.LoadPlugins(); }
			catch (Exception e) { 
				Console.WriteLine(e.ToString());
				SystemLog.Log(e.ToString());
				throw; 
			}
			allGlobalUsers = DeserializeBinaryDatabase<User>(Config.AppSettings.UserDBFileName);
			channels = DeserializeBinaryDatabase<Channel>(Config.AppSettings.ChannelDBFileName);
			Ops.AddRange(XMLConfig["botOps"].Split(';'));
			
			DateTime LastPurge = DateTime.Now;

			EternalSender = new Timer(SendMessageFromQueue, _sendForever, Timeout.Infinite, (long)IO.Message.chat_flood + 1);
			LaplacesDemon = new Timer(ProcessCommandFromQueue, _sendForever, 0, 100);

			WebClient w = new WebClient();
			//string InfoList = new StreamReader(w.OpenRead(Config.URLConstants.V1.AllData)).ReadToEnd();
			string MappingList = System.Web.HttpUtility.HtmlDecode(w.DownloadString(Config.URLConstants.V1.Mapping));
			APIMapping = JsonConvert.DeserializeObject<FListAPI.Mapping>(MappingList);

			//encoding strigns is so last year, let's have the fucking client interpret a fucking numerical map
			//TODO build FListAPI.Response.Mapping object...
			websocket = new WebSocket(string.Format("ws://{0}:{1}", XMLConfig["server"], XMLConfig["port"]));
			websocket.MessageReceived += Core.OnWebsocketMessage;
			websocket.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>(Core.OnWebsocketError);
			websocket.Open();
			Account.login(XMLConfig["account"], XMLConfig["password"]);

			_quitEvent.WaitOne(); //keep websockets open and blocks thread until tripped

			SystemLog.Log("Saving channel and user data to hard drive...");
			if (websocket.State == WebSocketState.Open) { Core.websocket.Close(); }
			while (IncomingMessageQueue.Count > 0) { try { ProcessCommandFromQueue(new object()); } catch (Exception e) { ErrorLog.Log("Exception in command processing during shutdown: " + e.Message); } }
			
			SaveAllSettingsBinary();
			
			foreach (Channel c in joinedChannels) { c.Dispose(); }
			foreach (Logging.LogFile l in ActiveUserLogs) { l.Dispose(); } //flushes and writes all extant user logs.
			SystemLog.Log("Shutting down.");
		}

		internal static void ProcessCommandFromQueue(object stateobject) {
			if ((DateTime.Now - LastPurge) > Config.AppSettings.DataPurgeAndBackupPeriod) {
				LastPurge = DateTime.Now;
				SaveAllSettingsBinary();
				SystemLog.Log("Settings and data autosaved.");
			}

			if (IncomingMessageQueue.Count > 0) {
				SystemCommand C = IncomingMessageQueue.Dequeue();
				try { Processor.GetType().GetMethod(C.OpCode, BindingFlags.NonPublic | BindingFlags.Static).Invoke(C, new object[] { C }); }
				catch (Exception FuckUp) { ErrorLog.Log(string.Format("Invocation of Method {0} failed:\n\t{1}\n\t{2}\t{3}", C.OpCode, FuckUp.Message, FuckUp.InnerException, C.Data)); }
			}
		}

		/// <summary>
		/// Function to Deserialize a ConcurrentSet T instance from BinarySerializer-produced files.
		/// </summary>
		/// <typeparam name="T">The inner type for the ConcurrentSet to deserialize, e.g. ConcurrentSet User</typeparam>
		/// <param name="DataBaseFileName">The name of the BinaryFormatted database file to be deserialized. Expects a List T.</param>
		/// <param name="ContainingFolder">Leave optional (null) to load from Config.AppSettings.DataPath (/data/); else, supply full path to containing folder</param>
		/// <exception cref="System.ArgumentException">Thrown when the TargetObject's type and the data inside the file do not match.</exception>
		private static ConcurrentSet<T> DeserializeBinaryDatabase<T>(string DataBaseFileName, string ContainingFolder = null, string Extension = ".dat"){
			SystemLog.Log("Loading " + typeof(T).Name + " Database...");
			ConcurrentSet<T> DeserializationProxy = new ConcurrentSet<T>();
			try{
				ContainingFolder = ContainingFolder ??  Config.AppSettings.DataPath;
				Stream s = File.OpenRead(Path.Combine(ContainingFolder, DataBaseFileName + Extension));
				BinaryFormatter bf = new BinaryFormatter();
				try { DeserializationProxy = (ConcurrentSet<T>)bf.Deserialize(s); }
				catch (System.Runtime.Serialization.SerializationException ex) { Core.ErrorLog.Log(String.Format("Could not deserialize database: {0} {1}", ex.Message, ex.StackTrace)); }
				catch (Exception e) { Core.ErrorLog.Log(String.Format("Error whilst deserializing database: {0} {1}", e.Message, e.StackTrace)); }
				s.Close();
				SystemLog.Log("Deserialized " + typeof(T).Name + " Database and loaded " + DeserializationProxy.Count + " entries.");
			}
			catch (FileNotFoundException) { } //Do Nothing ¯\_(ツ)_/¯
			catch (DirectoryNotFoundException) { Directory.CreateDirectory(Config.AppSettings.DataPath); }
			catch (UnauthorizedAccessException)
			{
				SystemLog.Log("Incapable of accessing user database directory");
				Console.WriteLine("Warning: Application is unable to access its user database in " + Config.AppSettings.DataPath +
				"'. Please ensure all proper permissions exist. Application may be unable to persist user database, leading to increased bandwith usage.", "Unable to load user database");
			}
			return DeserializationProxy;
		}
	   /*
		* internal static void SaveAllSettingsXML() {
		* 	try {
		* 		using (Stream fs = File.Create(Config.AppSettings.DataPath + Config.AppSettings.UserDBFileName)) {
		* 			XmlSerializer xmlf = new XmlSerializer(typeof(ConcurrentSet<User>));
		* 			xmlf.Serialize(fs, allGlobalUsers);
		* 			fs.Flush();
		* 		}
		* 	}
		* 	catch (Exception e) {
		* 		SystemLog.Log("WARNING: Failed to save XML user data to drive: " + e.Message);
		* 		ErrorLog.Log("WARNING: Failed to save XML user data to drive: " + e.Message);
		* 	}
		* 
		* 	try {
		* 		using (Stream fs = File.Create(Config.AppSettings.DataPath + Config.AppSettings.ChannelDBFileName)) {
		* 			XmlSerializer xmlf = new XmlSerializer(typeof(ConcurrentSet<Channel>));
		* 			xmlf.Serialize(fs, channels);
		* 			fs.Flush();
		* 		}
		* 	}
		* 	catch (Exception e) {
		* 		SystemLog.Log("WARNING: Failed to save XML channel data to drive: " + e.Message);
		* 		ErrorLog.Log("WARNING: Failed to save XML channel data to drive: " + e.Message);
		* 	}
		* }
		*/
		internal static void SaveAllSettingsBinary(string ContainingFolder = null, string Extension = ".dat") {
			ContainingFolder = ContainingFolder ?? Config.AppSettings.DataPath;
			BinaryFormatter bf = new BinaryFormatter();
			try {
				using (Stream fs = File.Create(Path.Combine(ContainingFolder, Config.AppSettings.UserDBFileName + Extension))) {
					foreach (User u in allGlobalUsers) { if (!u.Ignore) { allGlobalUsers.TryRemove(u); }  }
                    bf.Serialize(fs, allGlobalUsers);
					fs.Flush();
				}
			}
			catch (Exception e) {
				SystemLog.Log("WARNING: Failed to save binary user data to drive: " + e.Message);
				ErrorLog.Log("WARNING: Failed to save user binary data to drive: " + e.Message);
			}

			try {
				using (Stream fs = File.Create(Path.Combine(ContainingFolder, Config.AppSettings.ChannelDBFileName + Extension))) {
					bf.Serialize(fs, channels);
					fs.Flush();
				}
			}
			catch (Exception e) {
				SystemLog.Log("WARNING: Failed to save binary channel data to drive: " + e.Message);
				ErrorLog.Log("WARNING: Failed to save binary channel data to drive: " + e.Message);
			}
		}

		internal static void OnProcessExit(object sender, EventArgs e){
			SystemLog.Log("Saving channel and user data to hard drive...");
			Console.WriteLine("Saving channel and user data to hard drive...");
			if (Core.websocket.State == WebSocket4Net.WebSocketState.Open) { Core.websocket.Close(); } //Specification states it's perfectly alright to just close the connection without sending a 'logout' command of any kind.
			SaveAllSettingsBinary();
			Core.SystemLog.Log("Shutting down.");
		}

		private static void OnWebsocketClose(){
			SystemLog.Log("Closing connection...");
			//websocket.Send("");
		}

		internal static void OnWebsocketError(object sender, SuperSocket.ClientEngine.ErrorEventArgs e){
			//TODO write this thing for:
			//Error in Websocket: System.Net.Sockets.SocketException (0x80004005): A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond
			ErrorLog.Log(string.Format("Error in Websocket: " + e.Exception.Message));
			if(e.Exception.Message.Contains("A connection attempt failed because the connected party did not properly respond after a period of time")) {
				websocket.Close();
				Thread.Sleep(10000);
				websocket.Open();
				return;
			}
        }

		internal static void OnWebsocketMessage(object sender, WebSocket4Net.MessageReceivedEventArgs e){
			SystemCommand C = new SystemCommand(e.Message.ToString());
			if (C.OpCode == "PIN") { Core.websocket.Send("PIN"); return; }
			IncomingMessageQueue.Enqueue(C);
		}
		
		/// <summary> Fetches the corresponding User instance from the program's users database; creates (and registers) and returns a new one if no match is found.
		/// </summary>
		/// <param name="username">Username (string) to look for</param>
		/// <returns>User instance</returns>
		public static User getUser(string username){
			return allGlobalUsers.Count(x => x.Name == username) > 0 ? allGlobalUsers.First(n => n.Name == username) : new User(username);
		}

		///// <summary> Overloaded in order to immediately return User instances, as may happen...?
		///// </summary>
		///// <param name="user">User instance.</param>
		///// <returns>User instance</returns>
		//public static User getUser(User user) { return user; }

		///// <summary> Overloaded in order to immediately return Channel instances, as may happen...?
		///// </summary>
		///// <param name="channel">Channel instance.</param>
		///// <returns>channel instance</returns>
		//public static Channel getChannel(Channel channel) { return channel; }

		/// <summary>
		/// Fetches the corresponding channel instance from the List of all channels registered in CogitoSharp.Core; creates a new one (adding it to the central register) if no match is found.
		/// </summary>
		/// <param name="NameOrKey">The Channel Name (public channels) or Key (Private Channels) to look for.</param>
		/// <param name="overrideName">If no match is found and thus a new channel created, an overrideName is specified to ensure the channel isn't treated as public, and gets the right name</param>
		/// <returns>Channel Instance</returns>
		public static Channel getChannel(string NameOrKey, string overrideName = null){
			if (channels.Select(n => n.Key).Contains<string>(NameOrKey)) { return channels.First(o => o.Key == NameOrKey); }
			else {
				Channel c = new Channel(NameOrKey);
                if (overrideName != null) { 
					c.Name = overrideName;
					c.Key = NameOrKey; 
				}
				return c;
			}
			//return Core.channels.Count(x => x.Key == channel) > 0 ? Core.channels.First<Channel>(n => n.Key == channel) : new Channel(channel);
		}

		internal static void Message(string targetUser, string MessageBody, string Channel = "PM") {
			IO.Message m = new IO.Message();
			m.OpCode = Channel == "PM" ? "PRI" : "MSG";
			if (Channel != "PM") { m.Data["channel"] = Channel; }
			m.Data["recipient"] = targetUser;
			m.Body = MessageBody;
		}
	}
}
