using System.Collections.Generic;
using Newtonsoft.Json;

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

	public class CharacterDataAPIv1 : CharacterResponseBase {
		public Dictionary<int, ResponseMember> info { get; set; }
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

	public class Kinks { 
		public HashSet<string> Fave { get; set; }
		public HashSet<string> Maybe { get; set; }
		public HashSet<string> Yes { get; set; }
		public HashSet<string> No { get; set; }
	}

	public class CustomKink { 
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
}
