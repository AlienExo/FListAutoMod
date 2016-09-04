﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Timers;
using Newtonsoft.Json;

namespace CogitoMini.IO
{

	public class MessageEventArgs : EventArgs{ 
		
	}	

    class SystemCommand{
		internal protected string OpCode { get; set; }
		internal protected Dictionary<string, object> Data = new Dictionary<string, object>();

		internal protected User sourceUser = null;
		internal protected Channel sourceChannel = null;

		/// <summary>
		/// Sends the message by adding it to the OutgoingMessageQueue
		/// </summary>
		internal void Send() { Core.OutgoingMessageQueue.Enqueue(this); }

		/// <summary>
		/// Produces a string that fserv can interpret.
		/// </summary>
		/// <returns>A string "OPCODE {JSONKEY: "value", [...]}"</returns>
		public string ToServerString() {
			if (Data != null) { return OpCode.ToUpperInvariant() + " " + JsonConvert.SerializeObject(Data).ToString(); }
			else { return OpCode.ToUpperInvariant(); }
		}

		public SystemCommand(string rawmessage){
			OpCode = rawmessage.Substring(0, 3);
			if (rawmessage.Length > 4) {
				Data = JsonConvert.DeserializeObject<Dictionary<string, object>>(System.Net.WebUtility.HtmlDecode(rawmessage.Substring(4)));
				try {
					sourceChannel = Data.ContainsKey("channel") ? Core.getChannel((string)Data["channel"]) : null;
					if (Data.ContainsKey("character")) {
						string cleanName;
						if (OpCode == "JCH") { cleanName = JsonConvert.DeserializeObject<Dictionary<string, string>>(Data["character"].ToString())["identity"].ToString(); }
						else { cleanName = Data["character"].ToString(); }
						sourceUser = Core.getUser(cleanName);
					}
				}
				catch (Exception) { Core.SystemLog.Log(Data.ToString()); }
			}
			else { Data = null; }
		}

		public SystemCommand(Message parentMessage){
			OpCode = parentMessage.OpCode;
			Data = parentMessage.Data;
		}

		public SystemCommand() { }
	} //class SystemCommand

	class Message : SystemCommand{
		internal new User sourceUser = null;
		internal new Channel sourceChannel = null;
		internal AccessLevel AccessLevel = new AccessLevel();

		/// <summary> Maximum length (in bytes) of a channel message; longer and you gotta split it </summary>
		internal static int chat_max = 4096;
		/// <summary> Maximum length (in bytes) of a private message. </summary>
		internal static int priv_max = 50000;
		/// <summary> Minimum number of milliseconds to wait in between sending chat messages (flood avoidance)</summary>
		internal static int chat_flood = 550;
		
		/// <summary> Message body </summary>
		internal string Body{
			get { return Data["message"].ToString(); }
			set{ Data["message"] = value; }
		}

		internal string[] args { 
			get { return Body.Split(' '); } 
			set	{ Body = string.Join(" ", value); }
		}

		public Message(SystemCommand s){
			OpCode = s.OpCode;
			Data = s.Data;
			Body = Data["message"].ToString();
			sourceChannel = s.sourceChannel;
			sourceUser = s.sourceUser;
		}

		public Message(string messageBody, Message parentMessage) : base(parentMessage) {
			sourceUser = parentMessage.sourceUser;
			sourceChannel = parentMessage.sourceChannel;
			Body = messageBody;	
		}

		public Message() : base() { }

		/// <summary>
		/// Sends the message by adding it to the OutgoingMessageQueue
		/// </summary>
		internal new void Send(){
			if (sourceUser == null && sourceChannel == null) { throw new ArgumentNullException("Attempted to send a chat message with no user or channel specified"); }
			if (OpCode == null) { OpCode = sourceUser == null ? "MSG" : "PRI"; } //sets Opcode to MSG (send to entire channel) if no user is specified, else to PRI (only to user)
			if (sourceUser != null) { Data["recipient"] = sourceUser.Name; }
			if (sourceChannel != null) { Data["channel"] = sourceChannel.Key; }

			//This should, in theory, make sure we don't send any too-long messages.
			//y u no autosplit your buffer
			int MessageLength = System.Text.Encoding.UTF8.GetByteCount(Body);
			int MaxLength = OpCode == "MSG" ? Message.chat_max : Message.priv_max;
			if (MessageLength > MaxLength) {
				List<Message> messages = new List<Message>();
				Message subMessage = new Message("", this);
				messages.Add(this);
				for (int i = 0; MessageLength > MaxLength; i++){
					subMessage.Body.Insert(subMessage.Body.Length, Body[Body.Length - i].ToString());
					Body = Body.Substring(0, Body.Length - 1);
					MessageLength = System.Text.Encoding.UTF8.GetByteCount(Body);
					messages.Add(subMessage);
				}
				messages.ForEach(x => x.Send()); //If we did this recursively, the last subMessage would send first,[ chunks | to reversed  | leading ]
				messages = null;
			}
			base.Send();
		}

		internal int getByteLength(){ return (System.Text.Encoding.UTF8.GetByteCount(Body)); }

		/// <summary>
		/// Replies to the message by posting to the same user/channel where the Message originated
		/// </summary>
		/// <param name="replyText">Text to reply with.</param>
		/// <param name="forcePrivate">Should the message be sent as private regardless of parent message origin?</param>
		internal void Reply(string replyText, bool forcePrivate = true){ 
			Message reply = new Message(replyText, this);
			reply.OpCode = forcePrivate ? "PRI" : "MSG";
			reply.Send();
		}

		public override string ToString(){
			string _message;
			if (Data["message"].ToString().StartsWith("/me")) { _message = sourceUser.Name + Data["message"].ToString().Substring(3); }
			else {_message = sourceUser.Name + ": " + Data["message"].ToString();}
			return _message;
		}
	} //class Message

	internal class Logging{
		internal class LogFile : IDisposable{
			private System.Timers.Timer flushTimer = new System.Timers.Timer();
			private string rootFilename, extension, subdirectory;
			private long writeInterval;
			private DateTime creationDate = new DateTime();
			private FileStream logFileStream = null;
			private StreamWriter logger = null;
			private bool disposed = false;
			private bool timestamped = false;
			
			public void Log(string s, bool suppressPrint = false){
				if (logFileStream == null) { ReSetup(); } //effectively, file is set up on first write rather than on object creation
				s = string.Format("<{0}> -- {1}{2}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), s, Environment.NewLine);
				if (!suppressPrint) { Console.Write(s); }
				logger.Write(s);
			}

			public void Log(IO.Message m, bool suppressPrint = false) {
				if (logFileStream == null) { ReSetup(); } //effectively, file is set up on first write rather than on object creation
				string chanStr = m.OpCode == "PRI" ? "[PM]" : "(" + m.sourceChannel.Name + ") [" + m.Data["channel"] + "]";
				string s = string.Format("<{0}> -- {1} {2}{3}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), chanStr, m.ToString(), Environment.NewLine);
				if (!suppressPrint) { Console.Write(s); }
				logger.Write(s);
			}

			/// <summary>
			/// To be called when you've already processed the mesasge with a timestamp for the channel; just pass the message.ToString() result and it's logged 'raw'
			/// </summary>
			public void LogRaw(string s){
				if (logFileStream == null) { ReSetup(); } //effectively, file is set up on first write rather than on object creation
				Console.Write(s);
				logger.Write(s);
			}

			private void ReSetup() {
				string FilePath;
				FilePath = subdirectory.Length > 0 ? Path.Combine(Config.AppSettings.LoggingPath, subdirectory) : Config.AppSettings.LoggingPath;
				if (!Directory.Exists(FilePath)) {
					try {
						Directory.CreateDirectory(FilePath);
					}
					catch (Exception e) {
						Console.WriteLine(e.InnerException + "\n\tGetting Temp Path for log file instead...");
						FilePath = Path.GetTempPath();
					}
				}
				FilePath = timestamped ? Path.Combine(FilePath, DateTime.Today.ToString("yyyy_MM_dd") + "_" + rootFilename + extension) : Path.Combine(FilePath, rootFilename + extension);

				logFileStream = File.Open(FilePath, FileMode.Append, FileAccess.Write);
				logger = new StreamWriter(logFileStream);
				flushTimer.Interval = writeInterval;
				flushTimer.Elapsed += flushTimer_Elapsed;
				flushTimer.AutoReset = true;
				flushTimer.Start();
			}

			/// <summary>
			/// Creates a FileStream for writing to the logfile, and periodically (default: 10 sec.) flushes the buffer to preserve that data in event of failure. 
			/// Keeping the file open rathern than open -> append -> close aparently improves performance
			/// </summary>
			/// <param name="FileName">The Filename of the file to be logged to. Folder is automatically added.</param>
			/// <param name="subfolder">The folder below the root logging folder, if any, this log should be put into. Default is none.</param>
			/// <param name="writeInterval">The interval, in milliseconds, between calling Flush().</param>
			/// <param name="extension">The file extension for the log, default ".txt".</param>
			public LogFile(string FileName, string extension = ".txt", long writeInterval = Config.AppSettings.loggingInterval, string subdirectory = "", bool timestamped = false){
				creationDate = DateTime.Today;
				rootFilename = FileName;
				this.extension = extension;
				this.writeInterval = writeInterval;
				this.timestamped = timestamped;
				this.subdirectory = subdirectory;
			}

			void flushTimer_Elapsed(object sender, ElapsedEventArgs e){
				logger.Flush();
				if (DateTime.Today > creationDate) {    //if the logging runs past midnight, change file to next day
					flushTimer.Stop();
					logFileStream.Close();
					ReSetup();
					creationDate = DateTime.Today;
				}	
			}

			~LogFile(){ Dispose(true); }

			//implement IDisposable
			public void Dispose(){
				Dispose(true);
				GC.SuppressFinalize(this);
			}

			protected virtual void Dispose(bool disposing){
				if (!disposed)
				{
					if (disposing){
						//logger.Flush(); //todo make sure logs are complete??
						//logger.Dispose();
						flushTimer.Stop();
						flushTimer.Dispose();
						// Free other state (managed objects).
					}
					disposed = true;
				}
			}
		} //class LogFile
	} //class Logging
} //namespace IO