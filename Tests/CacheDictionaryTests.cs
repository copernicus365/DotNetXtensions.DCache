namespace DotNetXtensions.DCache.Tests;

public class CacheDictionaryTests
{
	/// <summary>
	/// As nice as a bunch of small unit-tests would be, testing this is
	/// difficult because it depends on changes of state. To mock those changes of
	/// state takes a lot of setup that has to 
	/// </summary>
	[Fact]
	public void BIGTest_Count_CountPurged_GetItemsEnumerator_ItemGet_AutoRemovesPurged_Etc()
	{
		var cd = getMockCacheDict(out DateTime now);

		// this WILL (Should) hit cd.CopyTo, then GetItems, etc. Test that out 
		// in debugging but no assertions, how would we. This DOES also mean
		// expired items WILL be purged! So ToArray, ToList, ToDictionary, etc
		// all DO purge expired items that haven't been removed yet, we like :)
		var d = cd.ToDictionary(kv => kv.Key, kv => kv.Value);

		True(cd.DictionariesAreEqual(d));

		double addMinutesToExpireApples =
			MockVals1["Apples"] + cd.ExpiresAfter.TotalMinutes;
		// let's mock that "Now" is 1 minute after our start time, and then add the expires time,
		// currently == 1 minute as well. This is exactly at threshold after which first two items
		// will drop off, `{ "Apples", 1 }` etc
		Equal(2.0, addMinutesToExpireApples);

		cd.GetDateTimeNow = () => now.AddMinutes(addMinutesToExpireApples - 0.001);
		Equal(5, cd.CountPurged());

		cd.GetDateTimeNow = () => now.AddMinutes(addMinutesToExpireApples);

		int countPurged = cd.CountPurged();

		Equal(5, cd.Count);
		Equal(3, countPurged);

		Equal(["Peaches", "Pears", "Pineapples"], cd.Keys.ToArray().OrderBy(n => n));
		Equal([3, 3, 5], cd.Values.ToArray().OrderBy(n => n));

		// Let's demonstrate
		// 1) TryGetValue works, 
		// 2) it DOES remove an expired item when found

		Equal(5, cd.Count);
		False(cd.TryGetValue("Apples", out int _val));
		Equal(4, cd.Count);

		True(cd.TryGetValue("Peaches", out _val));
		Equal(3, _val);

		// ------

		double addMinutesToExpirePears =
			MockVals1["Pears"] + cd.ExpiresAfter.TotalMinutes;

		cd.GetDateTimeNow = () => now.AddMinutes(addMinutesToExpirePears);

		cd.PurgeExpiredItems();

		countPurged = cd.CountPurged();

		Single(cd);
		Equal(1, countPurged);
		Equal(["Pineapples"], [.. cd.Keys]);
	}

	[Fact]
	public void AddAlreadyExistingKeyActsLikeSet_NOException()
	{
		var cd = getMockCacheDict(out DateTime now);

		string keyNm = "Peaches";
		int newVal = 88;

		Equal(5, cd.Count);
		Equal(3, cd[keyNm]);

		cd.Add(keyNm, newVal);

		Equal(5, cd.Count);
		Equal(newVal, cd[keyNm]);

		cd[keyNm] = newVal;
		cd.Add(keyNm, newVal);

		Equal(5, cd.Count);
		Equal(newVal, cd[keyNm]);

		// --- now DO add a new value, but then add it again

		keyNm = "Mango";
		newVal = 44;

		cd.Add(keyNm, newVal); // make sure to DO do Add first here
		cd[keyNm] = newVal;

		Equal(6, cd.Count);
		Equal(newVal, cd[keyNm]);
	}

	[Fact]
	public void AutoPurgeOnGet()
	{
		CacheDictionary<string, int> cd = getMockCacheDict(out DateTime now);
		cd.RunPurgeTS = TimeSpan.FromMinutes(1);
		cd.GetDateTimeNow = () => now;

		Equal(5, cd.Count);
		Equal(5, cd.Count); // intentional doublet? make sure Count doesn't affect anything? Ok, fine, but its a nothingburger

		True(cd.TryGetValue("Peaches", out int _val));
		Equal(3, _val);
		Equal(5, cd.Count);

		now = now.AddMinutes(4);
		DateTime nextPurgeDT = cd.ResetRunNextPurgeDT();

		Equal(now.AddMinutes(1), nextPurgeDT); // == cd.RunPurgeTS set above 

		Equal(5, cd.Count);
		False(cd.TryGetValue("Peaches", out _val));
		True(cd.TryGetValue("Pineapples", out _val));
		Equal(4, cd.Count);
	}

	[Fact]
	public void TriggerFullPurgeOnGet()
	{
		CacheDictionary<string, int> cd = getMockCacheDict(out DateTime now);
		cd.RunPurgeTS = TimeSpan.FromMinutes(1);

		Equal(5, cd.Count);

		DateTime nextPurgeDT = cd.ResetRunNextPurgeDT();
		// MUST run this while Now was plain Now, AFTER mock that its 4 mins later
		Equal(now.Add(cd.RunPurgeTS), nextPurgeDT); // == cd.RunPurgeTS set above 

		double minsToExpirePears = MockVals1["Pears"] + cd.RunPurgeTS.TotalMinutes;
		Equal(4.0, minsToExpirePears);
		cd.GetDateTimeNow = () => now.AddMinutes(minsToExpirePears);

		Equal(5, cd.Count);
		False(cd.TryGetValue("Peaches", out int _val));
		True(cd.TryGetValue("Pineapples", out _val));
		Single(cd);
	}

	/// <summary>
	/// 2 with 1 min, 2 with 3 min, 1 with 5 min
	/// </summary>
	static Dictionary<string, int> MockVals1 = new() {
		{ "Apples", 1 },
		{ "Oranges", 1 },
		{ "Peaches", 3 },
		{ "Pears", 3 },
		{ "Pineapples", 5 },
	};


	CacheDictionary<string, int> getMockCacheDict(out DateTime now)
	{
		DateTime nw = now = DateTime.Parse("2026-02-03 13:35:00"); // _getNowRoundedUp();
		DateTime nowP2 = now.AddMinutes(2);

		CacheDictionary<string, int> cd = new(TimeSpan.FromMinutes(1)) {

			// IMPORTANT NOTE:
			// for mocking, we MUST set this value to greater than highest 
			// mock time we want to test ({ "Pineapples", 5 }), otherwise 
			// purges will be firing when we don't want them to which will
			// fail the test state / make testing impossible
			RunPurgeTS = TimeSpan.FromMinutes(6)
		};

		foreach(var kv in MockVals1) {
			string ky = kv.Key;
			int val = kv.Value;

			DateTime fakeAddedTimeAfterNow = now.AddMinutes(val);

			//!!! we alter GetDateTimeNow WHILE adding (in this mock)
			// thus GetDateTimeNow *must* not be readonly, for this purpose
			// allows us to set anything we want, any snapshot of items (expired or not compared to a **final** "now"), etc
			cd.GetDateTimeNow = () => fakeAddedTimeAfterNow;
			cd.Add(ky, val);
		}

		cd.GetDateTimeNow = () => nw;

		return cd;
	}

	//DateTime _getNowRoundedUp(double addMins = 0)
	//	=> DateTime.Now.RoundUp(TimeSpan.FromMinutes(1)).AddMinutes(addMins);

}
