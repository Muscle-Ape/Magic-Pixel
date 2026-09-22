using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace Joy.Event
{
	public enum BIPlatform
	{
		None,
		TA,//ThinkingAnalytics,
		AF//AppsFlyer,
	}

	public static class BIEvents
	{
		static string FormatLog(Dictionary<string, object> content = null)
		{
			if (content != null)
			{
				StringBuilder builder = new StringBuilder();
				builder.Append("{");
				foreach (var item in content)
				{
					builder.Append($"\"{item.Key}\":\"{item.Value.ToString()}\", ");
				}
				builder.Append("}");
				return builder.ToString();
			}
			return "";
		}

		static string FormatLog(Dictionary<string, string> content = null)
		{
			if (content != null)
			{
				StringBuilder builder = new StringBuilder();
				builder.Append("{");
				foreach (var item in content)
				{
					builder.Append($"\"{item.Key}\":\"{item.Value}\", ");
				}
				builder.Append("}");
				return builder.ToString();
			}
			return "";
		}

		static Dictionary<string, DateTime> m_TimeEventRecords = new Dictionary<string, DateTime>();
		public static void Time(string eventName)
		{
			try
			{
#if hq_ta_event
				ThinkingAnalytics.ThinkingAnalyticsAPI.TimeEvent(eventName);
#else
				m_TimeEventRecords.Add(eventName, DateTime.Now);
#endif

			}
			catch (Exception ex)
			{
				EventLogEror(ex, eventName, null, BIPlatform.None);
			}
		}

		public static void Send(string eventName, Dictionary<string, object> content = null, BIPlatform platform = BIPlatform.TA)
		{
			try
			{
				EventLog(eventName, content, platform);

				switch (platform)
				{
					case BIPlatform.TA:
						{
#if hq_ta_event
							ThinkingAnalytics.ThinkingAnalyticsAPI.Track(eventName, content);
#endif
						}
						break;
					case BIPlatform.AF:
						{
							//增加time event的时长,暂时不打开
							// if (m_TimeEventRecords.TryGetValue(eventName, out var record))
							// {
							// 	int milliseconds = (DateTime.Now - record).Milliseconds;
							// 	if (content == null)
							// 	{
							// 		content = new Dictionary<string, object>();
							// 	}
							// 	content.Add("#duration", milliseconds);
							// 	m_TimeEventRecords.Remove(eventName);
							// }
#if hq_af_event
							AppsFlyerSDK.AppsFlyer.sendEvent(eventName, content?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString()));
#endif
						}
						break;
				}
			}
			catch (Exception ex)
			{
				EventLogEror(ex, eventName, content, platform);
			}
		}

		public static void UserSetOnce(Dictionary<string, object> content)
		{
#if hq_ta_event
			ThinkingAnalytics.ThinkingAnalyticsAPI.UserSetOnce(content);
#endif
			Joy.Debug.Log(FormatLog(content));
		}

		public static void SendAppsFlyer(string eventName, Dictionary<string, string> content = null)
		{
			try
			{
#if hq_af_event
				AppsFlyerSDK.AppsFlyer.sendEvent(eventName, content);
#endif
				Joy.Debug.Log(FormatLog(content));
			}
			catch (Exception ex)
			{
				EventLogEror(ex, eventName, null, BIPlatform.AF);
			}
		}

		private static void EventLog(string eventName, Dictionary<string, object> content = null, BIPlatform platform = BIPlatform.TA)
		{
			Joy.Debug.Log($"[Event] {platform.ToString()} eventName:{eventName}");
		}

		private static void EventLogEror(Exception ex, string eventName, Dictionary<string, object> content = null, BIPlatform platform = BIPlatform.TA)
		{
			string contentJson = FormatLog(content);
			Joy.Debug.LogError($"[Event] {platform.ToString()} eventName:{eventName} content:{contentJson} error:{ex.Message}\n{ex.StackTrace}");
		}
	}
}
