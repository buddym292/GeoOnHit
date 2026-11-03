using System;
using System.Collections.Generic;
using Modding;
using GlobalEnums;
using UnityEngine;

namespace GeoOnHit
{
    public class GeoOnHit : Mod, IGlobalSettings<GlobalSettings>
    {
        public static GlobalSettings Settings = new GlobalSettings();
        public override string GetVersion() => "1.3.0.0";

        private static GameObject geoPrefab;

        // 按敌人实例记录上次掉落时间
        private static readonly Dictionary<HealthManager, float> lastDropTimes = new Dictionary<HealthManager, float>();
        private const float PerEnemyCooldown = 0.3f;
        private static float lastCleanupTime = 0f;

        public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        {
            foreach (var pool in ObjectPool.instance.startupPools)
            {
                if (pool.prefab.name.Equals("Geo Small"))
                {
                    geoPrefab = pool.prefab;
                    break;
                }
            }

            if (geoPrefab == null)
                Modding.Logger.LogWarn("[GeoOnHit] 找不到 Geo Small 预制体，掉落将不会生效");

            On.HealthManager.TakeDamage += HealthManager_TakeDamage;
        }

	private void HealthManager_TakeDamage(
    		On.HealthManager.orig_TakeDamage orig,
    		HealthManager self,
    		HitInstance hitInstance)
	{
    		orig(self, hitInstance);

    		if (hitInstance.DamageDealt <= 0) return;
    		if (HeroController.instance == null || geoPrefab == null) return;

  		  bool isNail = hitInstance.AttackType == AttackTypes.Nail;
 		   bool isNailBeam = hitInstance.AttackType == AttackTypes.NailBeam;
    		if (!isNail && !(Settings.GeoFromNailBeam && isNailBeam)) return;

    		if (lastDropTimes.TryGetValue(self, out float last) && Time.time - last < PerEnemyCooldown)
        		return;
    		lastDropTimes[self] = Time.time;

    		int amount = CalculateGeoAmount(hitInstance.DamageDealt);
    		FlingGeo(self.gameObject.transform.position, amount);

    		if (Time.time - lastCleanupTime > 5f)
    		{
        		lastCleanupTime = Time.time;
        		CleanupStaleEntries();
    		}
	}

	private int CalculateGeoAmount(int damageDealt)
	{
    		int nailDmg = PlayerData.instance.nailDamage;

    		// 骨钉等级基础掉落：旧骨钉 → 纯粹骨钉
    		int baseGeo;
    		if (nailDmg >= 21)      baseGeo = 9;  // 纯粹骨钉
    		else if (nailDmg >= 17) baseGeo = 7;  // 带螺纹
    		else if (nailDmg >= 13) baseGeo = 5;  // 开槽
   		else if (nailDmg >= 9)  baseGeo = 4;  // 锋利
    		else                    baseGeo = 2;  // 旧骨钉

    		// 实际伤害相对骨钉基础伤害的倍率（含力量、亡者之怒等护符加成）
    		float multiplier = (float)damageDealt / nailDmg;
    		int amount = Mathf.RoundToInt(baseGeo * multiplier);

    		return Mathf.Clamp(amount, Settings.MinGeoPerHit, Settings.MaxGeoPerHit);
	}

        private void CleanupStaleEntries()
        {
            var toRemove = new List<HealthManager>();
            foreach (var kvp in lastDropTimes)
            {
                if (kvp.Key == null || Time.time - kvp.Value > 10f)
                    toRemove.Add(kvp.Key);
            }
            foreach (var key in toRemove)
                lastDropTimes.Remove(key);
        }

        private void FlingGeo(Vector3 origin, int geoCount)
        {
            for (int i = 0; i < geoCount; i++)
            {
                GameObject go = geoPrefab.Spawn(origin, Quaternion.Euler(Vector3.zero));

                Vector3 pos = go.transform.position;
                pos.x += UnityEngine.Random.Range(-0.25f, 0.25f);
                pos.y += UnityEngine.Random.Range(-0.25f, 0.25f);
                go.transform.position = pos;

                float speed = UnityEngine.Random.Range(23, 30);
                float angle = UnityEngine.Random.Range(80, 100);
                Vector2 velocity = default;
                velocity.x = speed * Mathf.Cos(angle * ((float)Math.PI / 180f));
                velocity.y = speed * Mathf.Sin(angle * ((float)Math.PI / 180f));

                Rigidbody2D rb2d = go.GetComponent<Rigidbody2D>();
                if (rb2d != null)
                    rb2d.velocity = velocity;
            }
        }

        public void OnLoadGlobal(GlobalSettings settings) => Settings = settings;
        public GlobalSettings OnSaveGlobal() => Settings;
    }

    public class GlobalSettings
    {
        public int MinGeoPerHit = 1;       // 单次掉落下限
        public int MaxGeoPerHit = 30;      // 单次掉落上限
        public bool GeoFromNailBeam = true; // 蜕变挽歌剑气是否也掉
    }
}