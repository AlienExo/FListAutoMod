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
		public IList<Item> items { get; set; }
	}

	public abstract class CharacterResponseBase { //can use to make V1 / V2 ambiguous object
		public string error;
		public string name;
		public int age;
	}

	public enum KinkValue : int { fave = 0, yes, maybe, no }

	public struct CustomKink {
		string name;
		string description;
		KinkValue choice;
		IList<string> children;
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

	public class CharacterDataAPIv1 {
		public string error { get; set; }
		public int id{ get; set; }
		public string name{ get; set; }
		public string description{ get; set; }
		public int views{ get; set; }
		public bool customs_first{ get; set; }
		public string custom_title{ get; set; }
		public bool is_self{ get; set; }
		public Dictionary<string, bool> settings{ get; set; }
		public IList<string> badges{ get; set; }
		public DateTime created_at{ get; set; }
		public DateTime updated_at{ get; set; }
		public Dictionary<int, KinkValue> kinks{ get; set; }
		public Dictionary<int, CustomKink> custom_kinks{ get; set; }
		public Infotags infotags{ get; set; }
		public Dictionary<int, InlineImage> inlines{ get; set; }
		public Dictionary<int, CharacterImage> images{ get; set; }
	}

	public class Infotags {
		[JsonProperty(PropertyName = "Age")]
		public int Age { get; set; }
		[JsonProperty(PropertyName = "Build")]
		public string Build { get; set; }
		[JsonProperty(PropertyName = "Gender")]
		public string Gender { get; set; }
		[JsonProperty(PropertyName = "Orientation")]
		public string Orientation { get; set; }
		[JsonProperty(PropertyName = "Position")]
		public string Position { get; set; }
		[JsonProperty(PropertyName = "Species")]
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

//	public class CustomKinkv2 { 
//		public string name { get; set; }
//		public string description { get; set; }
//	}
//
//	public class CharacterDataAPIv2 : CharacterResponseBase{ 
//		public string name { get; set; }
//		public int id { get; set; }
//		public int last_updated { get; set; }
//		public int created{ get; set; }
//		public Infotags infotags { get; set; }
//	}

	public class APIKink {
		[JsonProperty(PropertyName = "id")]
		int id{ get; set; }
		[JsonProperty(PropertyName = "name")]
		string name{ get; set; }
		[JsonProperty(PropertyName = "description")]
		string description{ get; set; }
		[JsonProperty(PropertyName = "group_id")]
		int group_id{ get; set; }
	}

	public class APIKinkGroup {
		[JsonProperty(PropertyName = "id")]
		string id{ get; set; }
		[JsonProperty(PropertyName = "name")]
		string name{ get; set; }
	}

	public class APIInfoTag {
		[JsonProperty(PropertyName = "id")]
		int id{ get; set; }
		[JsonProperty(PropertyName = "name")]
		string name{ get; set; }
		[JsonProperty(PropertyName = "type")]
		string ItemType{ get; set; }
		[JsonProperty(PropertyName = "list")]
		string list{ get; set; }
		[JsonProperty(PropertyName = "group_id")]
		int group_id{ get; set; }
	}

	public class APIInfoTagGroup {
		[JsonProperty(PropertyName = "id")]
		int id{ get; set; }
		[JsonProperty(PropertyName = "name")]
		string name{ get; set; }
	}

	public class APIInfolistItem {
		[JsonProperty(PropertyName = "id")]
		int id{ get; set; }
		[JsonProperty(PropertyName = "name")]
		string name{ get; set; }
		[JsonProperty(PropertyName = "value")]
		string value{ get; set; }
	}

	public class Mapping {
		[JsonProperty(PropertyName = "kinks")]
		IList<APIKink> kinks { get; set; }
		[JsonProperty(PropertyName = "kink_groups")]
		IList<APIKinkGroup> kink_groups { get; set; }
		[JsonProperty(PropertyName = "infotags")]
		IList<APIInfoTag> infotags { get; set; }
		[JsonProperty(PropertyName = "infotag_groups")]
		IList<APIInfoTagGroup> infotag_groups { get; set; }
		[JsonProperty(PropertyName = "listitems")]
		IList<APIInfolistItem> listitems { get; set; }
		[JsonProperty(PropertyName = "error")]
		string error { get; set; }
	}
}

