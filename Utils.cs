using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CogitoMini.Utils
{
    /// <summary>
    /// Mathematical utility functions
    /// </summary>
    public static class Math{
		private static string[] ones				= {"", "one ", "two ", "three ", "four ", "five ", "six ", "seven ", "eight ", "nine ", "ten ", "eleven ", "twelve ", "thirteen ", "fourteen ", "fifteen ", "sixteen ", "seventeen ", "eighteen ", "nineteen "};
		private static string[] _ones				= {"zero ", "one ", "two ", "three ", "four ", "five ", "six ", "seven ", "eight ", "nine "};
		private static string[] tens				= {"", "teen ", "twenty ", "thirty ", "fourty ", "fifty ", "sixty ", "seventy ", "eighty ", "ninety "};
		private static string[] units				= {"", "", "thousand ", "million ", "billion "};

		private static string[] HighScientificUnits = {"kilo ", "mega ", "giga ", "tera ", "peta ", "exa ", "zetta ", "yotta "};
		private static string[] LowScientificUnits	= {"milli ", "micro ", "nano ", "pico ", "femto ", "atto ", "zetto ", "yocto "};
		public const float Inches_to_cm		= 2.54f;
		public const float Fl_Oz_to_ml		= 29.5735296f;
		public const float Pounds_to_gram	= 453.59237f;

		/// <summary>
		/// Returns a float[] filled with values for a 'dampened spring' animation.
		/// </summary>
		/// <param name="start">Integer at which to start; x-position. Default: 0 (can be used for addition to start position)</param>
		/// <param name="amplitude">Amplitude of the spring, default 1f. Max value which it can reach.</param>
		/// <param name="damping">Level of damping, default 0.2f</param>
		/// <param name="tension">Tension of the spring, default 0.7f</param>
		/// <param name="precision">Number of data points to generate; the higher the number, the smoother the curve. Default: 50</param>
		/// <returns>A float[] with values describing the oscillation of the dampened spring.</returns>
		public static float[] dampenedSpringDelta(int start = 0, float amplitude = 1f, float damping = 0.2f, float tension = 0.7f, int precision = 50)
		{
			//dampened spring oscillation is preferable to straight-up sin wave.
			float position = -1f;
			float velocity = 0.5f;
			float[] deviations = new float[precision];
			for (int i = 0; i < precision; i++)
			{
				//insert amplitude somehow.
				velocity = velocity * (1f - damping);
				velocity -= (position - damping) * tension;
				position += velocity;
				deviations[i] = position;
			}
			float average = deviations.Average();
			for (int i = 0; i < precision; i++)
			{
				float res = deviations[i] - average;
				res *= amplitude;
				deviations[i] = res + start;
			}
			return deviations;
		}

		/// <summary>
		/// Transforms a number into its spoken representation, e.g. 123.45 to "one hundred and twenty three point fourty five"
		/// </summary>
		/// <typeparam name="T">The type of number suppled</typeparam>
		/// <param name="number">The number</param>
		/// <returns>A string with the number in spoken form.</returns>
		public static string numberToSentence<T>(T number) where T : struct, IComparable, IComparable<T>, IConvertible, IEquatable<T>, IFormattable {
			if (float.Parse(number.ToString()) == 0f) {return "zero";}
			string[] _numStr = number.ToString().Split('.');
			string output = "";

			string[] __numStr = StringManipulation.Chunk(_numStr[0], 3, false);
			for (int j = 0; j < __numStr.Length; j++){
				string chunk = __numStr[j];
				for (int k = 0; k < chunk.Length; k++){
					int current = int.Parse(chunk[k].ToString());
					int pos = chunk.Length - k;
					switch (pos){
						case 1:
							output += ones[current];
							break;

						case 2:
							int _current = int.Parse(chunk.Substring(k));
							if (_current <= 19) { output += ones[_current]; k++; }
							else { output += tens[current]; }
							break;

						case 3:
							output += ones[current];
							output += "hundred ";
							break;
					}
				}
				output += (__numStr.Length > 1) ? units[__numStr.Length - j] : "";
			}
			if (_numStr.Length > 1) {
				output += "point ";
				foreach (char c in _numStr[1]){ output += _ones[int.Parse(c.ToString())]; }
			}
			return output.TrimEnd(new char[]{' '});
		}

		/// <summary>
		/// Returns a random element from the IEnumerable T
		/// </summary>
		/// <typeparam name="T">The type of collection from which to get the item. Must implement IEnumerable</typeparam>
		/// <param name="source">The collection from which to randomly choose an item</param>
		/// <returns>A random item from object source</returns>
		public static T RandomChoice<T>(IEnumerable<T> source) {
			Random rnd = new Random();
			IList<T> list = source.ToList();
			int index = rnd.Next(list.Count);
			if (list != null) { return list[index]; }
			else {
				T result = default(T);
				int cnt = 0;
				foreach (T item in source) {
					cnt++;
					if (rnd.Next(cnt) == 0) { result = item; }
				}
				return result;
			}
		} //RandomChoice

		/// <summary>
		/// 
		/// </summary>
		public enum MeasurementUnit { Unknown, Length, Weight, Volume }

		/// <summary>
		/// Simple numeric struct to keep a measurement and its unit.
		/// </summary>
		/// <typeparam name="T">Numeric type of the measurement</typeparam>
		public class Measurement<T> where T : struct, IComparable, IConvertible, IComparable<T>, IEquatable<T>, IFormattable {
			T value;
			//string rootUnit;
			MeasurementUnit UnitType;

			public Measurement(T amount, string UnitName) {
				value = amount;
				try { Enum.TryParse<MeasurementUnit>(UnitName, out UnitType); }
				catch (ArgumentException) {
					UnitName = UnitName.ToLowerInvariant();
					switch (UnitName) {
						case "liter":
						case "liters":
						case "litre":
						case "litres":
							break;

						case "millilitre":
						case "milliliter":
							break;

						case "kilo":
						case "kilos":
							break;

						case "centimeter":
							break;

						case "meter":
							break;
						// -------------------------------------------------------------------------------------
						case "inch":
						case "inches":
							break;

						case "feet":
						case "foot":
							break;

						case "gallon":
						case "gallons":
							break;

						case "ounce":
						case "ounces":
							break;

						case "pound":
						case "pounds":
							break;
					}
				}
			}

			/*public float AsImperial() { 
				switch (UnitType) {
					
				}
			}
			*/	
		}

		/// <summary>
		/// Attempts to parse numbers from a sentence into regular english. Only works with english-language and correctly spelled senteces, sadly.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="numberSentence"></param>
		/// <returns></returns>
		public static T parseNumberFromWords<T>(string numberSentence) where T : struct, IComparable, IComparable<T>, IConvertible, IEquatable<T>, IFormattable{
			Regex Number = new Regex(@"\d{0,9}\.{0,1}\d{0,9}");
			string numberMatch = Number.Match(numberSentence).Groups[0].Value;
			
			try { double.Parse(numberMatch); }
			catch (Exception){
				throw;
			}
			numberSentence = numberSentence.Replace('-', ' ');
			numberSentence = numberSentence.Replace("ty", "");
			string[] splitNumberSentence = numberSentence.Split(' ');
			foreach (string s in splitNumberSentence){
				 
			}
			throw new NotImplementedException();
		}

		/// <summary>
		/// Takes a descriptive string, e.g. "They are between 5 and 10 inches tall" and attempts to return a number of type T. 
		/// When a range is detected, the arithmetic mean is returned.
		/// All data is converted to a standard metric unit before being returned as a Measurement instance.
		/// </summary>
		/// <typeparam name="T">The numeric type the function returns. Internally, numbers are handled as doubles...?</typeparam>
		/// <param name="TextToAnalyze">The text string from which data is supposed to be parsed</param>
		/// <param name="MeasureToParseAs">If known, the type of measurement to be parsed.</param>
		/// <returns> A Measurement<!--<T>--> instance with the result as type T and the unit in a string"/> A Measurement with numeric type T</returns>
		public static Measurement<T> parseMeasurementFromDescription<T>(string TextToAnalyze, MeasurementUnit MeasureToParseAs = MeasurementUnit.Unknown) where T : struct, IComparable, IComparable<T>, IConvertible, IEquatable<T>, IFormattable{
			//Measurement<T> Result = new Measurement<T>();
			string[] RangeIndicators = { "-", "/", " to ", " and " };
			string[] MetricIndicators = { "cm", "m", "km" };
			string[] ImperialIndicators = { "in", "inches", "inch", "feet", "foot", "\"", "'"};

			switch (MeasureToParseAs){
				case MeasurementUnit.Unknown:
				
				break;
				
				case MeasurementUnit.Length:
				
				break;
				
				case MeasurementUnit.Volume:

				break;
				
				case MeasurementUnit.Weight:
				
				break;
			}
			throw new NotImplementedException();
			//TODO: If regex doesn't find anything, try Parse Number From Words

			//return Result;
			//Imperial Length - inches, in, feet, f, ' "
			//Imperial Weight - 
			//Imperial Volume - gallon, quart, fl oz
			//Metric Length   - centimeter, meter, cm, m, km, etc etc
			//Metric weight   - gram, kilo, kg, g, ton 
			//metric volume   - 

			//range indicators- "to" "-" "/" ","
			//remove tokens per strip... or just collect 
		}

		public static double? Median<T>(this IEnumerable<T> source) {
			if (Nullable.GetUnderlyingType(typeof(T)) != null) { source = source.Where(x => x != null); }
			int count = source.Count();
			if (count == 0) { return null; }
			source = source.OrderBy(n => n);
			int midpoint = count / 2;
			if (count % 2 == 0) { return (Convert.ToDouble(source.ElementAt(midpoint - 1)) + Convert.ToDouble(source.ElementAt(midpoint))) / 2.0; }
			else { return Convert.ToDouble(source.ElementAt(midpoint)); }
		}

		public static double StDev(this IEnumerable<float> source) {
			double avg = source.Average();
			int nSource = source.Count();
			List<double> differences = new List<double>(nSource);
			foreach (float f in source) { differences.Add(System.Math.Pow(f - avg, 2)); } 
			return System.Math.Sqrt(differences.Sum() / nSource);
		}

		private static Random rng = new Random();
		public static void Shuffle<T>(this IList<T> list) {
			int n = list.Count;
			while (n > 1) {
				n--;
				int k = rng.Next(n + 1);
				T value = list[k];
				list[k] = list[n];
				list[n] = value;
			}
		}

	}// class Math

	public static class RegularExpressions{
		internal static Regex ProfileHTMLTags = new Regex(@"<span class=.*>(.*):</span>(.*)");
		internal static Regex AgeSearch = new Regex(@"(\d{1,9})");
		internal static Regex Numbers = new Regex(@"(\d{1,5}[\.,]?\d{0,2})");
		internal static Regex Dates = new Regex("(?<day>\\d{1,2})(\\s)*(/|.|(st|nd|rd|th))?(\\s)*(?<month>(\\d{1,2}|\\w{3,9}))");
	}

	public static class StringManipulation{

		/// <summary> Reverses a string into a string and not a char[] nightmare. What the eff, C#.</summary>
		/// <param name="s">String to be reversed</param>
		/// <returns>desrever eb ot gnirtS</returns>
		public static string ReverseString(string s){
			char[] arr = s.ToCharArray();
			Array.Reverse(arr);
			return new string(arr);
		} //ReverseString

		/// <summary> Divides a string into nChunks chunks of (roughly) equal size</summary>
		/// <param name="nChunks">Number of chunks to divide str into</param>
		/// <param name="str">string to divide into nChunks chunks</param>
		/// <param name="fwd">Determines if chunking is forward or reverse, e.g. XXXXXXXX into chunks of 3 can be "XXX XXX XX"(fwd) or "XX XXX XXX"(rev). Default is true.</param>
		/// <returns>A string[] containing the chunks</returns>
		public static string[] Chunk(int nChunks, string str, bool fwd = true) {
			int chunkLength = (int)System.Math.Ceiling((double)str.Length / nChunks);
			return Chunk(str, nChunks, fwd);
		}

		/// <summary> Divides a string str into an IEnumerable with elements of length chunkLength in it</summary>
		/// <param name="str">The string to chunk</param>
		/// <param name="chunkLength">The length of chunks to divide into</param>
		/// <param name="fwd">Determines if chunking is forward or reverse, e.g. XXXXXXXX into chunks of 3 can be "XXX XXX XX"(fwd) or "XX XXX XXX"(rev). Default is true.</param>
		/// <returns>A string[] containing the chunks</returns>
		public static string[] Chunk(string str, int chunkLength, bool fwd = true){
			if (str == null){ return new string[0]; }
			int chunks = (int)System.Math.Ceiling((double)str.Length/chunkLength);
			if (fwd == true) { 
				string[] result = Enumerable.Range(0, chunks)
					.Select(i => str.Substring(i * chunkLength, (i * chunkLength + chunkLength <= str.Length) ? chunkLength : str.Length - i * chunkLength)).ToArray<string>(); 
				return result;
			} //if
			else{
				string _str = ReverseString(str);
				string[] result = Enumerable.Range(0, chunks)
					.Select(i => ReverseString(_str.Substring(i * chunkLength, (i * chunkLength + chunkLength <= str.Length) ? chunkLength : str.Length - i * chunkLength))).Reverse().ToArray<string>(); 
				return result;
			} //else
		} //Chunk
	} //StringManipulation

	public static class Collections {
		public class Counter<T> : IEnumerable<T> {
			private SortedDictionary<T, int> _dict;

			public Counter(IEnumerable<T> data): this() { Add(data); }
			public Counter() { _dict = new SortedDictionary<T, int>(); }

			public bool Contains(T item) { return _dict.ContainsKey(item); }

			public void Add(T item) {
				if (_dict.ContainsKey(item)) { _dict[item]++; }
				else { _dict[item] = 1; }
			}

			public void Add(IEnumerable<T> items) { foreach (var item in items) { Add(item); } }

			public void Remove(T item) {
				if (!_dict.ContainsKey(item))
					throw new ArgumentException();
				if (--_dict[item] == 0) { _dict.Remove(item); }
			}

			// Return the last value in the multiset
			public T Peek() {
				if (!_dict.Any())
					throw new NullReferenceException();
				return _dict.Last().Key;
			}

			// Return the last value in the multiset and remove it.
			public T Pop() {
				T item = Peek();
				Remove(item);
				return item;
			}

			public IEnumerator<T> GetEnumerator() {
				foreach (var kvp in _dict)
					for (int i = 0; i < kvp.Value; i++)
						yield return kvp.Key;
			}

			IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

			public override string ToString() {
				IOrderedEnumerable<KeyValuePair<T, int>> data = _dict.OrderBy(n => n.Value);
				return string.Join(", ", data.Select(n => "'" + n.Key + "': " + n.Value)).TrimEnd(new char[] { ',', ' ' }); 
			}

			public string ToString(int limit) {
				IOrderedEnumerable<KeyValuePair<T, int>> data = _dict.OrderBy(n => n.Value);
				return string.Join(", ", data.Take(limit).Select(n => n.Key + " (" + n.Value + ")")).TrimEnd(new char[] { ',', ' ' }); 
			}

			public string[] AsArray() { return _dict.Select(n => n.Key + " (" + n.Value + ")").ToArray(); }


			public int Count { get { return _dict.Keys.Count;  } }

		}//Counter
	}//collections
}