using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Fartheim
{
	public class Farter : MonoBehaviour
	{
		static string[] BoneRoots = new string[] { "Hip", "Hips", "l_hip" };
		AudioSource AS;
		Character Char;
		bool Crouching;
		float TimeSinceLastFart;
		float FartTimerDelay;
		ParticleSystem PS;
		float SizeScalar = 1;

		public void Awake()
		{
			Char = GetComponentInChildren<Character>();
			AS = gameObject.AddComponent<AudioSource>();
			AS.playOnAwake = false;
			AS.maxDistance = Plugin.FartNoiseRange.Value;
			AS.rolloffMode = AudioRolloffMode.Linear;
			AS.spatialBlend = 1f;
			AS.outputAudioMixerGroup = Traverse.Create(typeof(AudioMan)).Field<AudioMan>("m_instance").Value.m_ambientMixer;

			Transform psparent = null;
			if (Char)
			{
				if (!gameObject.name.StartsWith("Skeleton") && !gameObject.name.StartsWith("Blob"))
				{
					string bone = null;
					SkinnedMeshRenderer smr = gameObject.GetComponentInChildren<SkinnedMeshRenderer>();
					if (smr)
					{
						if (gameObject.name.StartsWith("Boar") || gameObject.name.StartsWith("Wolf")) bone = "Pelvis";
						else if (gameObject.name.StartsWith("Deer")) bone = "L Thigh";
						else if (gameObject.name.StartsWith("Dragon")) bone = "Hips.001";
						else if (gameObject.name.StartsWith("Draugr")) bone = "LeftHip";
						else if (gameObject.name.StartsWith("Eikthyr")) bone = "Bone.009";
						else if (gameObject.name.StartsWith("Hatchling")) bone = "Spine1";
						else if (gameObject.name.StartsWith("Surtling")) bone = "mixamorig:Hips";
						else if (gameObject.name.StartsWith("Troll")) bone = "Spine0";
						if (bone != null)
						{
							psparent = smr.bones.FirstOrDefault(t => t.name == bone);
							if (!psparent) psparent = transform;
						}
						else
						{
							psparent = smr.bones.FirstOrDefault(t => BoneRoots.Contains(t.name));
						}
					}
				}
			}

			if (!psparent) psparent = transform;
			GameObject go = GameObject.Instantiate(Plugin.FartSystemPrefab, psparent);
			PS = go.GetComponent<ParticleSystem>();

			CapsuleCollider cc = gameObject.GetComponentInChildren<CapsuleCollider>();
			if (cc)
			{
				double r = cc.radius;
				double h = cc.height;

				double v = 4.0 / 3.0 * Math.PI * Math.Pow(r, 3.0);
				h -= 2 * r;
				if (h > 0) v += Math.PI * Math.Pow(r, 2.0);

				SizeScalar = (float)Math.Sqrt(v);
			}
			else
			{
				SphereCollider sc = gameObject.GetComponentInChildren<SphereCollider>();
				if (sc)
				{
					double v = 4.0 / 3.0 * Math.PI * Math.Pow(sc.radius, 3.0);
					SizeScalar = (float)Math.Sqrt(v);
				}
			}

			if (SizeScalar != 1f)
			{
				var main = PS.main;
				main.startSpeedMultiplier = SizeScalar;
				main.startSizeMultiplier = SizeScalar;
				var vol = PS.velocityOverLifetime;
				vol.speedModifierMultiplier = SizeScalar;
			}
		}

		void Fart()
		{
			List<AudioClip> acs = (Char && Char.InWater()) ? Plugin.WetFarts : Plugin.DryFarts;
			int index;
			if (Char && Char.IsPlayer() && Plugin.BunsOfSteel.Value) index = Mathf.Min(acs.Count - 1, (int)(TimeSinceLastFart / Plugin.MaxFartTime.Value * acs.Count));
			else index = Mathf.Min(acs.Count - 1, (int)(UnityEngine.Random.value * acs.Count));
			AudioClip ac = acs[index];
			AS.pitch = (0.75f + UnityEngine.Random.value * 0.5f) / SizeScalar;
			AS.PlayOneShot(ac, Mathf.Clamp(TimeSinceLastFart / Plugin.MaxFartTime.Value, 0.25f, 1f));
			FartTimerDelay = ac.length + 1f + UnityEngine.Random.value;
			TimeSinceLastFart = 0;

			PS.transform.rotation = Quaternion.LookRotation(-transform.forward, transform.up);
			var main = PS.main;
			main.duration = ac.length;
			PS.Play();
		}

		public void Update()
		{
			if (!Player.m_localPlayer) return;

			if (!Char || !Char.IsPlayer() || !Plugin.BunsOfSteel.Value)
			{
				if (FartTimerDelay <= 0)
				{
					TimeSinceLastFart += Time.deltaTime;
					float r = UnityEngine.Random.value * Plugin.MaxFartTime.Value;
					if (TimeSinceLastFart >= r) Fart();
					else FartTimerDelay += 0.5f + UnityEngine.Random.value;
				}
				else
				{
					FartTimerDelay -= Time.deltaTime;
					if (TimeSinceLastFart > 0) TimeSinceLastFart += Time.deltaTime;
				}
			}
			else TimeSinceLastFart += Time.deltaTime;

			if (Char && Char.IsCrouching())
			{
				if (!Crouching)
				{
					// crouching squeezes one out
					Crouching = true;
					Fart();
				}
			}
			else if (Crouching) Crouching = false;

			if (PS)
			{
				var fol = PS.forceOverLifetime;
				if (Char && (Char.InInterior() || (Char.IsPlayer() && (Char as Player).InShelter())))
				{
					fol.x = 0;
					fol.z = 0;
				}
				else
				{
					Vector3 w = EnvMan.instance.GetWindForce();
					fol.x = w.x * 3;
					fol.z = w.z * 3;
				}
			}
		}
	}

	[HarmonyPatch(typeof(Character))]
	public class CharacterPatches
	{
		[HarmonyPostfix]
		[HarmonyPatch("Awake")]
		public static void AwakePostfix(Character __instance)
		{
			if (!Plugin.OnlyPlayers.Value || __instance is Player) __instance.gameObject.AddComponent<Farter>();
		}
	}

	[HarmonyPatch(typeof(RandomFlyingBird))]
	public class RandomFlyingBirdPatches
	{
		[HarmonyPostfix]
		[HarmonyPatch("Awake")]
		public static void StartPostfix(RandomFlyingBird __instance)
		{
			if (Plugin.ShouldBirdsFart.Value) __instance.gameObject.AddComponent<Farter>();
		}
	}
}
