using System;
using System.Collections.Generic;

namespace CogitoMini
{
    class Config{
		internal static class URLConstants{
			const string AvatarURI = "";
			internal const string Domain = @"https://www.f-list.net";

			internal static class V1 {
				internal const string FListAPI = Domain + @"/json/api/";
				internal const string CharacterInfo = FListAPI + @"character-info.php";         // Requires three parameters, "name", "account" and "ticket". 
				internal const string ProfileText = FListAPI + @"character-get.php";            // Requires one parameter, "name". 
				internal const string CharacterKinks = FListAPI + @"character-kinks.php";       // Requires one parameter, "name". 
				internal const string IncomingFriendRequests = FListAPI + "request-list.php";
				internal const string OutgoingFriendRequests = FListAPI + "request-pending.php";

				internal const string GetTicket = Domain + @"/json/getApiTicket.php";
				internal const string Login = Domain + @"/action/script_login.php";
				internal const string ReadLog = Domain + @"/fchat/getLog.php?log=";
				internal const string ViewNote = Domain + @"/view_note.php?note_id=";
				internal const string UploadLog = Domain + @"/json/api/report-submit.php";
				internal const string ViewHistory = Domain + @"/history.php?name=";
				internal const string SendNote = Domain + @"/json/notes-send.json";
				internal const string SearchFields = Domain + @"/json/chat-search-getfields.json?ids=true";
				internal const string ProfileImages = Domain + @"/json/profile-images.json";
				internal const string KinkList = Domain + @"/json/api/kink-list.php";
				internal const string CharacterPage = Domain + "/c/";
			}

			internal static class V2 {
				internal const string FListAPI = Domain + @"/api/v2/";
				internal const string GetTicket = FListAPI + "auth";
				internal const string CharacterInfo = FListAPI + @"character/data";
            }

			//internal const string StaticDomain = @"https://static.f-list.net";
			//internal const string CharacterAvatar = StaticDomain + @"/images/avatar/";
			//internal const string EIcon = StaticDomain + @"/images/eicon/";
			internal const string ProfileRoot = Domain + @"/c/";
		}

		internal static class AppSettings{
			internal const string Version = "1.1.3";
            internal static readonly TimeSpan reconnectionStagger = new TimeSpan(0, 0, 10);
			internal static readonly TimeSpan ticketLifetime = new TimeSpan(0, 29, 0);
			internal static readonly TimeSpan userProfileRefreshPeriod = new TimeSpan(0, 1, 0, 0);
			internal const long loggingInterval = 5000;
			internal static readonly string AppPath = AppDomain.CurrentDomain.BaseDirectory;
			internal static readonly string LoggingPath = AppPath + @"logs\";
			internal static readonly string PluginsPath = AppPath + @"plugins\";
			internal static readonly string DataPath = AppPath + @"data\";
			internal const string UserDBFileName = "Users";
			internal const string ChannelDBFileName = "Channels";
			internal const string MasterKey = "1eccda8e49a5f5aa10a5b2bcf58514ff9791c426"; // 0 x Query Exiv Obe Exec TzW
			internal const string TriggerPrefix = ".";
			internal const int MessageBufferSize = 1024;
			internal static readonly string[] IgnoreCommands = {"LIS", "NLN", "STA", "ORS", "FLN", "CHA", "MSG"};
			internal static UnderageReponse DefaultResponse = UnderageReponse.Alert;
			internal const string RedirectOperator = "=>";
			internal static readonly TimeSpan DataPurgeAndBackupPeriod = new TimeSpan(0, 10, 0);
		}

		//internal static Dictionary<string, Delegate> AITriggers = new Dictionary<string, Delegate>();
		internal static Dictionary<string, CogitoPlugin> AITriggers = new Dictionary<string, CogitoPlugin>();

		internal static void RegisterPluginTrigger(string Trigger, CogitoPlugin Plugin){
			if (!Trigger.StartsWith(AppSettings.TriggerPrefix)) { Trigger.Insert(0, AppSettings.TriggerPrefix); }
			AITriggers.Add(Trigger, Plugin);
			Core.SystemLog.Log(String.Format("Added trigger {0} for Delegate {1}", Trigger, Plugin.Name));
		}
	}
}
