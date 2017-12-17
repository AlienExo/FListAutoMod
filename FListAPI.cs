using System.Collections.Generic;
using Newtonsoft.Json;
using System;

namespace CogitoMini.FListAPI {
	public class Item {
		public int id { get; set; }
		public string name { get; set; }
		public string value { get; set; }
	}

	public class ResponseMember {
		public string group { get; set; }
		public List<Item> items { get; set; }
	}

	public class CharacterResponseBase {
		public string error { get; set; }
	}

	public struct APIKink {
		int id;
		string name;
		string description;
		int group_id;
	}

	public struct APIKinkGroup {
		int id;
		string name;
	}

	public struct APIInfoTag {
		int id;
		string name;
		string type;
		List<string> list;
		int group_id;
	}

	public struct APIInfolistItem {
		int id;
		string name;
		string value;
	}

	public enum KinkValue : int { fave = 0, yes, maybe, no }

	public struct CustomKink {
		string name;
		string description;
		KinkValue choice;
		List<string> children;
	}

	public class InlineImage {
		public string hash;
		public string extension;
		public bool nsfw;
	}

	public class CharacterImage {
		public int image_id;
		public string extension;
		public int height;
		public int width;
		public string description;
		public int sort_order;
	}

	public class CharacterDataAPIv1 : CharacterResponseBase {
		public int id{ get; set; }
		public string name{ get; set; }
		public string description{ get; set; }
		public int views{ get; set; }
		public bool customs_first{ get; set; }
		public string custom_title{ get; set; }
		public bool is_self{ get; set; }
		public Dictionary<string, bool> settings{ get; set; }
		public List<string> badges{ get; set; }
		public DateTime created_at{ get; set; }
		public DateTime updated_at{ get; set; }
		public Dictionary<int, KinkValue> kinks{ get; set; }
		public Dictionary<int, CustomKink> custom_kinks{ get; set; }
		public Dictionary<int, string> infotags{ get; set; }
		public Dictionary<int, InlineImage> inlines{ get; set; }
		public Dictionary<int, CharacterImage> images{ get; set; }
	}

	public class Infotags {
		public int Age { get; set; }
		public string Build { get; set; }
		public string Gender { get; set; }
		public string Orientation { get; set; }
		public string Position { get; set; }
		public string Species { get; set; }

		[JsonProperty(PropertyName = "Body type")] 
		public string BodyType { get; set; }
		[JsonProperty(PropertyName = "Desired RP length")]
		public string RPLength{get; set;}
		[JsonProperty(PropertyName = "Desired RP method")]
		public string RPMethod {get; set;}
		[JsonProperty(PropertyName = "Desired post length")]
		public string PostLength{get; set;}
		[JsonProperty(PropertyName = "Dom/Sub Role")]
		public string DomSub {get; set;}
		[JsonProperty(PropertyName = "Furry preference")]
		public string FurryPref {get; set;}
		[JsonProperty(PropertyName = "Height/Length")]
		public string Height {get; set;}
		[JsonProperty(PropertyName = "Post Perspective")]
		public string PostPerspective { get; set; }

		public Dictionary<string, string> ToDictionary() {
			Dictionary<string, string> output = new Dictionary<string, string>();
			output["age"] = Age.ToString();
			output["build"] = Build;
			output["gender"] = Gender;
			output["orientation"] = Orientation;
			output["position"] = Position;
			output["species"] = Species;
			output["body type"] = BodyType;
			output["desired rp length"] = RPLength;
			output["desired rp method"] = RPMethod;
			output["desired post length"] = PostLength;
			output["dom/sub role"] = DomSub;
			output["furry preference"] = FurryPref;
			output["height/length"] = Height;
			output["post perspective"] = PostPerspective;
			return output;
		}
	}

	public class CustomKinkv2 { 
		public string name { get; set; }
		public string description { get; set; }
	}

	public class CharacterDataAPIv2 : CharacterResponseBase{ 
		public string name { get; set; }
		public int id { get; set; }
		public int last_updated { get; set; }
		public int created{ get; set; }
		public Infotags infotags { get; set; }
	}

	internal static class Response {
		static Dictionary<string, object> Mapping = new Dictionary<string, object>();
	}
	
}

