using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.ktgame.iap.server
{
	public static class SerializableDictionary
	{
		public static Dictionary<TK, TV> FromJson<TK, TV>(string json)
		{
			JsonDictionary<TK, TV> dictionary = JsonUtility.FromJson<JsonDictionary<TK, TV>>(json);
			return dictionary.ToDictionary();
		}

		public static string ToJson<TK, TV>(Dictionary<TK, TV> dict, bool prettyPrint = false)
		{
			JsonDictionary<TK, TV> serialized = JsonDictionary<TK, TV>.FromDictionary(dict);
			return JsonUtility.ToJson(serialized, prettyPrint);
		}

		[Serializable]
		private class JsonDictionary<TK, TV>
		{
			[SerializeField] private List<TK> keys = new List<TK>();
			[SerializeField] private List<TV> values = new List<TV>();

			public void Add(TK key, TV value)
			{
				keys.Add(key);
				values.Add(value);
			}

			public Dictionary<TK, TV> ToDictionary()
			{
				if (keys.Count != values.Count)
				{
					Debug.LogError("JSON Dictionary's key and value arrays have different lengths. Cannot convert to native dictionary.");
				}

				Dictionary<TK, TV> result = new Dictionary<TK, TV>();
				for (int i = 0; i < keys.Count; i++)
				{
					result.Add(keys[i], values[i]);
				}

				return result;
			}

			public static JsonDictionary<TK, TV> FromDictionary(Dictionary<TK, TV> dict)
			{
				JsonDictionary<TK, TV> result = new JsonDictionary<TK, TV>();

				foreach (KeyValuePair<TK, TV> pair in dict)
				{
					result.Add(pair.Key, pair.Value);
				}

				return result;
			}
		}
	}
}
