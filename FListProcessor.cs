using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using CogitoMini.IO;
using Newtonsoft.Json.Linq;

namespace CogitoMini
{
    /// <summary>
    /// Contains all methods that process FList server/client commands
    /// </summary>
    internal sealed class FListProcessor
	{
		/// <summary>This command requires chat op or higher. Request a character's account be banned from the server.
		/// Send  As: ACB { "character": string }</summary>
		public static void ACB(SystemCommand C) { }

		/// <summary>Sends the client the current list of chatops.
		/// Received: ADL { "ops": [string] }</summary>
		internal static void ADL(SystemCommand C) {
			List<string> AllOpsData = ((JArray)C.Data["ops"]).ToObject<List<string>>();
			foreach (string Op in AllOpsData) {
				User _Op = Core.getUser(Op);
				Core.globalOps.Add(_Op);
			}
		}

		/// <summary>The given character has been promoted to chatop. 
		/// Received: AOP { "character": string }
		/// Send  As: AOP { "character": string }</summary>
		internal static void AOP(SystemCommand C) { }

		/// <summary>This command requires chat op or higher. Requests a list of currently connected alts for a characters account. 
		/// Send  As: AWC { "character": string }</summary>
		internal static void AWC(SystemCommand C) { }

		/// <summary>Incoming admin broadcast. 
		/// Received: BRO { "message": string }
		/// Send  As: BRO { "message": string } (as if) </summary>
		internal static void BRO(SystemCommand C) { }

		/// <summary>This command requires channel op or higher. Request the channel banlist.
		/// Send  As: CBL { "channel": string } </summary>
		internal static void CBL(SystemCommand C) { }

		/// <summary> This command requires channel op or higher. Bans a character from a channel. 
		/// Send  As: CBU {"character": string, "channel": string}
		/// Received: CBU {"operator":string,"channel":string,"character":string}</summary>
		internal static void CBU(SystemCommand C) {
			string action = string.Format("{0} has banned {1} from {2}", C.Data["operator"], C.Data["character"], C.sourceChannel);
			C.sourceChannel.ChannelModLog.Log(action);
			C.sourceChannel.Log(action, true);
		}

		/// <summary>  Create a private, invite-only channel. 
		/// Send  As: CCR { "channel": string } </summary>
		internal static void CCR(SystemCommand C) { }

		/// <summary>Alerts the client that that the channel's description has changed. This is sent whenever a client sends a JCH to the server. 
		/// Received: CDS { "channel": string, "description": string }
		/// Send  As: CDS { "channel": string, "description": string }</summary>
		internal static void CDS(SystemCommand C) { C.sourceChannel.Description = C.Data["description"].ToString(); }

		/// <summary> Sends the client a list of all public channels.
		/// Send  As: CHA
		/// Received: CHA { "channels": [object] } </summary>
		///  CHA {"channels":[{"name":"Gay Males","mode":"both","characters":0}, [...] } ] }
		internal static void CHA(SystemCommand C) {
			try {
				var AllChannelData = JArray.Parse(C.Data["channels"].ToString());
				var _AllChannelData = AllChannelData.ToObject<List<Dictionary<string, object>>>();
				for (int k = 0; k < _AllChannelData.Count; k++) {
					Dictionary<string, object> currentChannel = _AllChannelData[k];
					Channel ch = new Channel(currentChannel["name"].ToString());
					ch.mode = (ChannelMode)Enum.Parse(typeof(ChannelMode), currentChannel["mode"].ToString());
                }
			}
			catch (Exception e) { Core.ErrorLog.Log(String.Format("Error whilst parsing channel list: {0}\n\t{1} - {2}", e.Message, e.StackTrace, e.Data)); }
            //TODO Entry point for all auto-joins, now that we know the channels
            new IO.SystemCommand("ORS").Send();            
		}

		/// <summary>  Invites a user to a channel. Sending requires channel op or higher.
		/// Send  As: CIU { "channel": string, "character": string }
		/// Received: CIU { "sender":string,"title":string,"name":string } </summary>
		internal static void CIU(SystemCommand C) {
			Channel ch = Core.getChannel(C.Data["title"].ToString());
			Core.SystemLog.Log(String.Format("Joining channel '{0}' by invitation of user '{1}'", C.Data["title"].ToString(), C.Data["sender"].ToString()));
			ch.Join();
		}

		/// <summary> This command requires channel op or higher. Kicks a user from a channel. 
		/// Received: CKU {"operator":string,"channel":string,"character":string}
		/// Send  As: CKU { "channel": string, "character": string }</summary>
		internal static void CKU(SystemCommand C) {
			string action = string.Format("{0} has kicked {1} from '{2}'", C.Data["operator"], C.Data["character"], C.sourceChannel);
			C.sourceChannel.ChannelModLog.Log(action);
			C.sourceChannel.Log(action, true);
		}

		/// <summary> This command requires channel op or higher. Promotes a user to channel operator.
		/// Received: COA {"character":string, "channel":string}
		/// Send  As: COA { "channel": string, "character": string }</summary>
		internal static void COA(SystemCommand C) {
			string action = string.Format("{0} has promoted {1} to operator status in '{2}'", C.Data["operator"], C.Data["character"], C.sourceChannel);
			C.sourceChannel.ChannelModLog.Log(action);
			C.sourceChannel.Log(action, true);
			C.sourceUser.isMod = true;
			if (!C.sourceChannel.Mods.Contains(C.sourceUser)) { C.sourceChannel.Mods.Add(C.sourceUser); }
		}

		/// <summary> Gives a list of channel ops. Sent in response to JCH.
		/// Received: COL { "channel": string, "oplist": [string] }
		/// Send  As: COL { "channel": string }</summary>
		internal static void COL(SystemCommand C) {
			#if DEBUG	
			Console.WriteLine("Modlist for '" + C.sourceChannel.Name + "': " + C.Data["oplist"]);
			#endif
			foreach (string s in (JArray)C.Data["oplist"]) {
				User u = Core.getUser(s);
				u.isMod = true;
                C.sourceChannel.Mods.Add(u); 
			}	 
		}

		/// <summary> After connecting and identifying you will receive a CON command, giving the number of connected users to the network.
		/// Received: CON { "count": int }</summary>
		internal static void CON(SystemCommand C) { 
			using (FileStream fs = File.Open(Config.AppSettings.LoggingPath + "Connections.log", FileMode.Append)) {
				StreamWriter fsw = new StreamWriter(fs);
				DateTime Now = DateTime.Now;
				fsw.Write(String.Format("{0}\t{1}\t{2}\t{3}\t{4}\tusers connected\r\n", Now.ToString("yyyy-MM-dd"), Now.ToString("HH:mm:ss"), Core.XMLConfig["server"], Core.XMLConfig["port"], C.Data["count"].ToString()));
				fsw.Flush();
			}
		}

		/// <summary> This command requires channel op or higher. Demotes a channel operator (channel moderator) to a normal user.
		/// Send  As: COR { "channel": string, "character": string }
		/// Received: COR {"character":"character_name", "channel":"channel_name"}</summary>
		internal static void COR(SystemCommand C) {
			string action = string.Format("{0} has been demoted from operator status in '{1}'", C.sourceUser.Name, C.sourceChannel);
			C.sourceChannel.ChannelModLog.Log(action);
			C.sourceChannel.Log(action, true);
			C.sourceUser.isMod = false;
			if (C.sourceChannel.Mods.Contains(C.sourceUser)) { C.sourceChannel.Mods.Remove(C.sourceUser); }
		}

		/// <summary> This command is admin only. Creates an official channel.
		/// Send  As: CRC { "channel": string }</summary>
		internal static void CRC(SystemCommand C) { }

		/// <summary> This command requires channel op or higher. Set a new channel owner.
		/// Received: CSO {"character":"string","channel":"string"}
		/// Send  As: CSO {"character":"string","channel":"string"}</summary>
		internal static void CSO(SystemCommand C) {
			string action = string.Format("Ownership of channel '{0}' has been transferred to {1}", C.sourceChannel, C.Data["character"]);
			C.sourceChannel.ChannelModLog.Log(action);
			C.sourceChannel.Log(action, true);
		}

		/// <summary> This command requires channel op or higher. Temporarily bans a user from the channel for 1-90 minutes. A channel timeout.
		/// Send  As: CTU { "channel":string, "character":string, "length":int }
		/// Received: CTU {"operator":"string","channel":"string","length":int,"character":"string"}</summary>
		internal static void CTU(SystemCommand C) {
			string action = string.Format("{0} has been suspended from {1} for {2} minutes by {3}", C.Data["character"], C.sourceChannel, C.Data["length"], C.Data["operator"]);
			C.sourceChannel.ChannelModLog.Log(action);
			C.sourceChannel.Log(action, true);
		}

		/// <summary> This command requires channel op or higher. Unbans a user from a channel.
		/// Send  As: CUB { channel: "channel", character: "character" }</summary>
		internal static void CUB(SystemCommand C) { }

		/// <summary> This command is admin only. Demotes a chatop (global moderator).
		/// Received: DOP { "character": character }
		/// Send  As: DOP { "character": string }</summary>
		internal static void DOP(SystemCommand C) { }

		/// <summary> Indicates that the given error has occurred.
		/// Received: ERR {"message": "string", "number": int}</summary>
		internal static void ERR(SystemCommand C) { Core.ErrorLog.Log(String.Format("F-List Error {0} : {1}", C.Data["number"], C.Data["message"])); }

		/// <summary> Search for characters fitting the user's selections. Kinks is required, all other parameters are optional.
		/// Send  As: FKS { "kinks": [int], "genders": [enum], "orientations": [enum], "languages": [enum], "furryprefs": [enum], "roles": [enum] }
		/// Received: FKS { "characters": [object], "kinks": [object] }</summary>
		internal static void FKS(SystemCommand C) { }

		/// <summary> Sent by the server to inform the client a given character went offline.
		/// Received: FLN { "character": string }</summary>
		internal static void FLN(SystemCommand C) { 
			C.sourceUser.Status = Status.offline;
			IEnumerable<Channel> cs = Core.joinedChannels.Where(x => x.Users.Contains(C.sourceUser));
			string byeString = C.sourceUser.Name + " has disconnected from the server";
            foreach (Channel c in cs) {
				if (c.Mods.Contains(C.sourceUser)) { c.ChannelModLog.Log(byeString, true); }
				c.Log(byeString);
			}
			Core.allGlobalUsers.Remove(C.sourceUser);	
		}

		/// <summary> Initial friends list.
		/// Received: FRL { "characters": [string] }</summary>
		internal static void FRL(SystemCommand C) { }

		/// <summary> Server hello command. Tells which server version is running and who wrote it.
		/// Received: HLO { "message": string }</summary>
		internal static void HLO(SystemCommand C) {
			Core.OwnUser = Core.getUser(Core.XMLConfig["character"]);
			new IO.SystemCommand("CHA").Send();
		}

		/// <summary> Initial channel data. Received in response to JCH, along with CDS.
		/// Received: ICH { "users": [object], "channel": string, "title": string, "mode": enum }
		/// ICH {"users": [{"identity": "Shadlor"}], "channel": "Frontpage", mode: "chat"}</summary>
		internal static void ICH(SystemCommand C) {
			IList<string> Users = ((JArray)C.Data["users"]).ToObject<List<Dictionary<string, string>>>().Select(n => n.Values.ToArray()[0]).ToList(); //List<Dictionary<string, string>> //TODO custom JSON fix...
			foreach (string u in Users) { C.sourceChannel.Users.Add(new User(u)); }
		}

		internal static void IDN(SystemCommand C) { }

		/// <summary> A multi-faceted command to handle actions related to the ignore list. 
		/// The server does not actually handle much of the ignore process, as it is the client's responsibility to block out messages it recieves from the server if that character is on the user's ignore list.
		/// Received: IGN { "action": string, "characters": [string] | "character":object }
		/// </summary>
		internal static void IGN(SystemCommand C) { }

		/// <summary>Indicates the given user has joined the given channel. This may also be the client's character.
		///	Received: JCH { "channel": string, "character": object, "title": string }
		///	Send  As: JCH { "channel": string } </summary>
		internal static void JCH(SystemCommand C) {
			JObject JoinData = (JObject)C.Data["character"];
			string us = JoinData["identity"].ToString();
            C.sourceChannel.Users.Add(new User(us));
			C.sourceChannel.Log(string.Format("User '{0}' joined Channel '{1}'", us, C.sourceChannel.Name));
			if (us == Core.OwnUser.Name) {
				Core.joinedChannels.Add(C.sourceChannel);
				C.sourceChannel.joinIndex = Core.joinedChannels.IndexOf(C.sourceChannel);
				return;
			}
			if (C.sourceChannel.minAge != 0) { C.sourceChannel.CheckAge(C.sourceUser.Name); }
			if (C.sourceChannel.Mods.Contains(C.sourceUser)) { ProcessModMessageQueue(C.sourceUser); }
		}

		/// <summary> Kinks data in response to a KIN client command.
		/// Received: KID { "type": enum, "message": string, "key": [int], "value": [int] }</summary>
		internal static void KID(SystemCommand C) { }

		/// <summary> This command requires chat op or higher. Request a character be kicked from the server.
		/// Send  As: KIK { "character": string }
		/// </summary>
		internal static void KIK(SystemCommand C) { }

		/// <summary> Request a list of a user's kinks.
		/// Send  As: KIN { "character": string }</summary>
		internal static void KIN(SystemCommand C) { }

		/// <summary> An indicator that the given character has left the channel. This may also be the client's character.
		/// Received: LCH { "channel": string, "character": character }
		/// Send  As: LCH { "channel": string }</summary>
		internal static void LCH(SystemCommand C) {
			//Channel ch = Core.getChannel(C.Data["channel"].ToString()); //"title" would be the channel's name, which in case of private channels can collide!
			string us = C.Data["character"].ToString();
			C.sourceChannel.Log(string.Format("User '{0}' left Channel '{1}'", us, C.sourceChannel.Name));
			C.sourceChannel.Users.Remove(Core.getUser(us));
			if (us == Core.OwnUser.Name) { 
				Core.joinedChannels.Remove(C.sourceChannel);
				C.sourceChannel.joinIndex = -1;	
			}
		}

		/// <summary> Sends an array of *all* the online characters and their gender, status, and status message.
		/// Received: LIS { characters: [object] }</summary>
		///LIS {"characters":[["Zeus Keraunos","Male","online",""],["Dionysos Thyrsos","Male","online","... Uncle, you have it bad."],["Bill Cypher","None","online",""],["Uvaxstra","Male","online",""]]}
		/// /!\ OBVIOUSLY A HUGE POTENTIAL PERFORMANCE SINK /!\
		internal static void LIS(SystemCommand C) {
			//No fuck this huge performance sink. I'll get user data when I -need- it. Like, on channel entry.
			/*try{
			 *	List<List<string>> UserData = Newtonsoft.Json.JsonConvert.DeserializeObject<List<List<string>>>(C.Data["characters"].ToString());
			 *	foreach (List<string> currentUser in UserData) {
			 *		User u = Core.getUser(currentUser[0]);
			 *		u.Gender = currentUser[1];
			 *		u.Status = (Status)Enum.Parse(typeof(Status), currentUser[2]);
			 *		
			 *	}
			 *}
			 *catch (InvalidCastException) { 
			 *	Core.ErrorLog.Log("Could not cast contents of LIS message to List<List<string>> - See dump below: "); 
			 *	Core.ErrorLog.Log(C.Data["characters"].ToString());
			 *	Core.ErrorLog.Log("\t\tDump complete");	
			 *}
			 */
		}

		/// <summary> A roleplay ad is received from a user in a channel.
		/// Received: LRP { "channel": "", "message": "", "character": ""}</summary>
		internal static void LRP(SystemCommand C) { }

		/// <summary> Sending/Receiving Messages in a channel
		/// Received: MSG { "character": string, "message": string, "channel": string }
		/// Send  As: MSG { "channel": string, "message": string }</summary>
		internal static void MSG(SystemCommand C) {
			Message m = new Message(C);
			m.sourceChannel.MessageReceived(m);
			ProcessPossibleCommand(m);
		}

		/// <summary> A user connected.
		/// Received: NLN { "identity": string, "gender": enum, "status": enum }</summary>
		internal static void NLN(SystemCommand C) {
			User u = Core.getUser(C.Data["identity"].ToString());
			u.Status = (Status)Enum.Parse(typeof(Status), C.Data["status"].ToString());
			u.Gender = C.Data["gender"].ToString();
        }

		/// <summary> Gives a list of open private rooms.
		/// Received: ORS { "channels": [object] } 
		/// e.g. "channels": [{"name":"ADH-300f8f419e0c4814c6a8","characters":0,"title":"Ariel's Fun Club"}] etc. etc.
		/// Send  As: ORS</summary>
		internal static void ORS(SystemCommand C) {
            Newtonsoft.Json.Linq.JArray AllChannelData = Newtonsoft.Json.Linq.JArray.Parse(C.Data["channels"].ToString());
			if (AllChannelData.Count > 0) {
				try{
					List<Dictionary<string, object>> _AllChannelData = AllChannelData.ToObject<List<Dictionary<string, object>>>();
					for (int l = 0; l < _AllChannelData.Count; l++) {
						Dictionary<string, object> currentChannel = _AllChannelData[l]; //removal of whole-list lookup for, uh, about 100 public channels and 1000+ privates...
						string cTitle = currentChannel["title"].ToString();
                        if (Core.channels.Count(x => x.Name == cTitle) == 0) { Channel ch = new Channel(cTitle, currentChannel["name"].ToString(), int.Parse(currentChannel["characters"].ToString())); }
					}
				}
				catch (Exception ex) { Core.ErrorLog.Log(String.Format("Private Channel parsing failed:\n\t{0}\n\t{1}\n\t{2}", ex.Message, ex.InnerException, ex.StackTrace)); }
			}
			new IO.SystemCommand("STA { \"status\": \"online\", \"statusmsg\": \"Running CogitoMini v" + Config.AppSettings.Version + "\" }").Send();
			foreach (string cn in Core.XMLConfig["autoJoin"].Split(';')) { if (Core.channels.Count(x => x.Name == cn) > 0) { Core.channels.First(y => y.Name == cn).Join(); } }
		}

		/// <summary> Profile data commands sent in response to a PRO client command. 
		/// Received: PRD { "type": enum, "message": string, "key": string, "value": string }</summary>
		internal static void PRD(SystemCommand C) { }

		/// <summary> Private Messaging
		/// Received: PRI { "character": string, "message": string }
		/// Send  As: PRI { "recipient": string, "message": string }</summary>
		internal static void PRI(SystemCommand C) {
			Message message = new Message(C);
			try{ 
				message.sourceUser.MessageReceived(message);
				ProcessPossibleCommand(message);
            }
			catch (Exception ex) { Core.ErrorLog.Log(String.Format("Error parsing message from {0}: {1} {2} {3}", message.sourceUser.Name, message.ToString(), ex.Message, ex.StackTrace)); }
		}

		/// <summary> Requests some of the profile tags on a character, such as Top/Bottom position and Language Preference.
		/// Send  As: PRO { "character": string }</summary>
		internal static void PRO(SystemCommand C) { }

		/// <summary> This command requires chat op or higher. Reload certain server config files
		/// Send  As: RLD { "save": string }
		/// </summary>
		internal static void RLD(SystemCommand C) { }

		/// <summary> Roll dice or spin the bottle.
		/// Send  As: RLL { "channel": string, "dice": string }
		/// </summary>
		internal static void RLL(SystemCommand C) { }

		/// <summary> Change room mode to accept chat, ads, or both.
		/// Received: RMO {"mode": enum, "channel": string}
		/// Send  As: RMO {"channel": string, "mode": enum}</summary>
		internal static void RMO(SystemCommand C) { }

		/// <summary> This command requires channel op or higher. Sets a private room's status to closed or open.
		/// Send  As: RST { "channel": string, "status": enum }</summary>
		internal static void RST(SystemCommand C) { }

		/// <summary> Real-time bridge. Indicates the user received a note or message, right at the very moment this is received.
		/// Received: RTB { "type": string, "character": string }</summary>
		internal static void RTB(SystemCommand C) { }

		/// <summary> This command is admin only. Rewards a user, setting their status to 'crown' until they change it or log out.
		/// Send  As: RWD { "character": string }</summary>
		internal static void RWD(SystemCommand C) { }

		/// <summary> Alerts admins and chatops (global moderators) of an issue.
		/// Send  As: SFC { "action": "report", "report": string, "character": string }</summary>
		/// Received: SFC {action:"string", moderator:"string", character:"string", timestamp:"string"}
		internal static void SFC(SystemCommand C) { }

		/// <summary> A user changed their status
		/// Received: STA { status: "status", character: "channel", statusmsg:"statusmsg" }
		/// Send  As: STA { "status": enum, "statusmsg": string }</summary>
		internal static void STA(SystemCommand C) {
			Status newStatus = (Status)Enum.Parse(typeof(Status), C.Data["status"].ToString());
			C.sourceUser.Status = newStatus;
			C.sourceUser.StatusMessage = C.Data["statusmsg"].ToString();
			if (C.sourceUser.isMod && (int)newStatus < 3) { ProcessModMessageQueue(C.sourceUser); }
		}

		/// <summary> An informative autogenerated message from the server.
		/// Received: SYS { "message": string, "channel": string }</summary>
		internal static void SYS(SystemCommand C) { }

		/// <summary> This command requires chat op or higher. Times out a user for a given amount minutes.
		/// Send  As: TMO { "character": string, "time": int, "reason": string }</summary>
		internal static void TMO(SystemCommand C) { }

		/// <summary> "user x is typing/stopped typing/has entered text" for private messages.
		/// Send  As: TPN { "character": string, "status": enum }
		/// Received: TPN { "character": string, "status": enum }</summary>
		internal static void TPN(SystemCommand C) { }

		/// <summary> This command requires chat op or higher. Unbans a character's account from the server.
		/// Send  As: UBN { "character": string }</summary>
		internal static void UBN(SystemCommand C) { }

		/// <summary> Informs the client of the server's self-tracked online time, and a few other bits of information
		/// Received: UPT { "time": int, "starttime": int, "startstring": string, "accepted": int, "channels": int, "users": int, "maxusers": int }</summary>
		internal static void UPT(SystemCommand C) {	}

		internal enum Permissions : int{
			Admin = 1, chatop = 2, chanop = 4, helpdeskchat = 8, helpdeskgeneral = 16, moderationsite = 32, reserved = 64, grouprequests = 128,
			newsposts = 256, changelog = 512, featurerequests = 1024, bugreports = 2048, tags = 4096, kinks = 8192, developer = 16384, tester = 32768,
			subscriptions = 65536, formerstaff = 131072
		};

		//priv_max: Maximum number of bytes allowed with PRI.
		//lfrp_max: Maximum number of bytes allowed with LRP.
		//lfrp_flood: Required seconds between LRP messages.
		//chat_flood: Required seconds between MSG messages.
		//permissions: Permissions mask for this character.
		//chat_max: Maximum number of bytes allowed with MSG.	
		/// <summary> Variables the server sends to inform the client about server variables.</summary>
		internal static void VAR(SystemCommand C) {
			//TODO: check format
			switch ((string)C.Data["variable"]) {
				case "msg_flood":
				case "chat_flood":
					IO.Message.chat_flood = (int)(float.Parse(C.Data["value"].ToString()) * 1000); //Multiplying by 1000 to convert from seconds to miliseconds
					Core.EternalSender.Change(1000, IO.Message.chat_flood);
					Core.SystemLog.Log("SYS: Send interval auto-adjusted to interval of " + (Message.chat_flood / 1000f) + " seconds. Starting EternalSender...");
					break;
				
				case "chat_max":
					IO.Message.chat_max = int.Parse(C.Data["value"].ToString());
					break;
				
				case "priv_max":
					IO.Message.priv_max = int.Parse(C.Data["value"].ToString());
					break;
			}
		}
		
		internal static void ProcessPossibleCommand(Message m) {
			AccessPath accesspath = new AccessPath();
			string TargetMethod = m.args[0];
			
			if (m.sourceChannel != null) {
				//if (m.sourceChannel.Mods.Select(n => n._Name).Contains(m.sourceUser._Name)) { m.AccessLevel++; }
				if (m.sourceChannel.Mods.Contains(m.sourceUser)) { m.AccessLevel++; }
				accesspath = AccessPath.ChannelOnly;
			}
			else { accesspath = AccessPath.PMOnly; }

			if (TargetMethod.StartsWith(Config.AppSettings.TriggerPrefix) && Config.AITriggers.ContainsKey(TargetMethod)) {
				m.args = m.args.Skip(1).ToArray(); //remove Trigger 
				if (m.args.Length >= 2) {
					int chanIndex = -1;
					if (m.args[m.args.Length - 2] == Config.AppSettings.RedirectOperator && int.TryParse(m.args[m.args.Length - 1], out chanIndex)) {
						if (chanIndex <= Core.joinedChannels.Count) { m.sourceChannel = Core.joinedChannels[chanIndex]; }
						m.args = m.args.Take(m.args.Length - 2).ToArray();
					}
				}
				if (Core.globalOps.Contains(m.sourceUser)) { m.AccessLevel = AccessLevel.GlobalOps; }
				if (Core.Ops.Select(n => n.ToLowerInvariant()).Contains(m.sourceUser._Name)) { m.AccessLevel = AccessLevel.RootOnly; }

				try {
					CogitoPlugin AIMethod = Config.AITriggers[TargetMethod];
					if (m.AccessLevel >= AIMethod.AccessLevel && accesspath >= AIMethod.AccessPath) {
						if (m.AccessLevel >= AccessLevel.ChannelOps) {
							if (m.sourceChannel != null) { m.sourceChannel.ChannelModLog.Log(string.Format("Executing command {0} by order of {1} [{2}], channel {3}. Args: {4}", TargetMethod, m.sourceUser.Name, m.AccessLevel, m.sourceChannel.Name, m.Body)); }
							else { Core.ModLog.Log(string.Format("Executing command {0} by order of {1} [{2}], via PM. Args: '{3}'", TargetMethod, m.sourceUser.Name, m.AccessLevel, m.Body)); }
						}
						AIMethod.PluginMethod(m);
					}
					else { m.Reply(string.Format("You do not have the neccessary access permissions to execute {0} in channel {1}.", TargetMethod, m.sourceChannel.Name)); }
				}
				catch (KeyNotFoundException NoMethod) { Core.ErrorLog.Log(string.Format("Invocation of Bot Method {0} failed, as the method is not registered in the AITriggers.Triggers dictionary or does not exist:\n\t{1}\n\t{2}", m.OpCode, NoMethod.Message, NoMethod.InnerException)); }
				catch (TargetException NoMethod) { Core.ErrorLog.Log(string.Format("Invocation of Bot Method {0} failed, as the method does not exist:\n\t{1}\n\t{2}", m.OpCode, NoMethod.Message, NoMethod.InnerException)); }
				catch (ArgumentException WrongData) { Core.ErrorLog.Log(string.Format("Invocation of Bot Method {0} failed, due to a wrong argument:\n\t{1}\n\t{2}", m.OpCode, WrongData.Message, WrongData.InnerException)); }
				catch (Exception FuckUp) { Core.ErrorLog.Log(string.Format("Invocation of Bot Method {0} failed due to an unexpected error:\n\t{1}\n\t{2}", m.OpCode, FuckUp.Message, FuckUp.InnerException)); }
			}

			else { OnChatMessage(m); } //raise MessageEvent to signal to plugins ~eine nachricht has arrived~
		}

		internal static void ProcessModMessageQueue(User u) {
			IEnumerable<Channel> IsModOf = Core.joinedChannels.Where(x => x.Mods.Contains(u));
			Dictionary<Channel, List<Incident>> data = new Dictionary<Channel, List<Incident>>();
			foreach (Channel c in IsModOf) {
				List<Incident> cis = new List<Incident>();
				while (c.modMessageQueue.Count > 0) { cis.Add(c.modMessageQueue.Dequeue()); }
				data[c] = cis;
			}
			if (data.Values.Sum(n => n.Count) == 0) { return; }

			data = data.Where(n => n.Value.Count > 0).ToDictionary(n => n.Key, n => n.Value);
			string FullMessage = "This is an automated message.\nWelcome back. In your absence, there have been {0} incidents requiring moderator attention, of which {1} have expired (user has left channel).";
			int totalIncidents = 0, expiredIncidents = 0;
			foreach (KeyValuePair<Channel, List<Incident>> Item in data) {
				IEnumerable<Incident> current = Item.Value.Where(n => Item.Key.Users.Select(x => x.Name).Contains(n.Subject));
				IEnumerable<Incident> expired = Item.Value.Where(n => !current.Contains(n));
				int expiredChanCount = expired.Count();
				int currentChanCount = current.Count();
				totalIncidents += (expiredChanCount + currentChanCount);
				expiredIncidents += expiredChanCount;
				FullMessage += string.Format("\n\t{0}: {1} current incident(s) ({2} expired). {3}", Item.Key.Name, currentChanCount, expiredChanCount, string.Join("\n\t\t", current.Select(n => n.ToString())));
				FullMessage += "\n";
				foreach (Incident i in expired) { Item.Key.ChannelModLog.Log("Expired incident: " + i.ToString(), true);} 
			}
			FullMessage = string.Format(FullMessage, totalIncidents, expiredIncidents);
			u.Message(FullMessage);
		}

		internal delegate void ChatMessageEventHandler(Message m);
		internal static event ChatMessageEventHandler ChatMessage;
		internal static void OnChatMessage(Message m) { if (m != null) { ChatMessage(m); } }
	}
}
