using System.Linq;
using CGame;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CGame.Animation.Tests
{
    public sealed class Ak12AdsContractTests
    {
        private const string Ak12PrefabPath = "Assets/Art/Weapon/AK12/AK12Weapon.prefab";
        private const string Ak12DefinitionPath =
            "Assets/Settings/Gameplay/WeaponDefinition/AK12/AK12WeaponDefinition.asset";
        private const string Ak12ProceduralProfilePath =
            "Assets/Art/Weapon/Profile/AK12/AK12ProceduralBoneProfile.asset";

        [Test]
        public void AdsRuntimeState_ReversesFromItsCurrentWeight()
        {
            AdsRuntimeState state = default;

            state.Advance(true, KTransform.Identity, 0.25f, 1.8f, 3f, EaseMode.EaseInOut);
            Assert.That(state.AimingWeight, Is.EqualTo(0.45f).Within(0.0001f));

            state.Advance(false, KTransform.Identity, 0.1f, 1.8f, 3f, EaseMode.EaseInOut);
            Assert.That(state.AimingWeight, Is.EqualTo(0.27f).Within(0.0001f));
        }

        [Test]
        public void Ak12AdsConfiguration_UsesTheFrozenInitialValues()
        {
            AdsLayerSettings settings = ScriptableObject.CreateInstance<AdsLayerSettings>();
            try
            {
                Assert.That(settings.AimingSpeed, Is.EqualTo(1.8f));
                Assert.That(settings.AimPointSpeed, Is.EqualTo(3f));
                Assert.That(settings.PositionBlend, Is.EqualTo(new Vector3(0.404f, 0.394f, 0.384f)));
                Assert.That(settings.RotationBlend, Is.EqualTo(new Vector3(0.414f, 0.399f, 0.404f)));
                Assert.That(settings.CameraBlend, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }

            Object definition = AssetDatabase.LoadAssetAtPath<Object>(Ak12DefinitionPath);
            Assert.That(definition, Is.Not.Null);
            SerializedObject serializedDefinition = new SerializedObject(definition);
            Assert.That(serializedDefinition.FindProperty("aimFov").floatValue, Is.EqualTo(40f));
        }

        [Test]
        public void Ak12WeaponAimPoint_IsAUniqueFiniteForwardFacingPrefabMarker()
        {
            GameObject prefab = PrefabUtility.LoadPrefabContents(Ak12PrefabPath);
            try
            {
                Transform[] aimPoints = prefab.GetComponentsInChildren<Transform>(true)
                    .Where(candidate => candidate.name == "WeaponAimPoint")
                    .ToArray();

                Assert.That(aimPoints, Has.Length.EqualTo(1));
                Transform aimPoint = aimPoints[0];
                Assert.That(IsFinite(aimPoint.position), Is.True);
                Assert.That(IsFinite(aimPoint.rotation), Is.True);
                Assert.That(aimPoint.forward.sqrMagnitude, Is.GreaterThan(0.999f));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        [Test]
        public void Ak12ProceduralProfile_PlacesAdsBetweenViewAndAdditiveWithCameraAndWeaponBones()
        {
            BoneProfile profile = AssetDatabase.LoadAssetAtPath<BoneProfile>(Ak12ProceduralProfilePath);
            Assert.That(profile, Is.Not.Null);
            profile.Validate(profile.Rig);
            WeaponBoneProfileValidator.ValidateAk12(profile);
            Assert.That(profile.Layers.Select(layer => layer.GetType()), Is.EqualTo(new[]
            {
                typeof(PoseSamplerLayerSettings),
                typeof(AttachHandLayerSettings),
                typeof(ViewLayerSettings),
                typeof(AdsLayerSettings),
                typeof(AdditiveLayerSettings),
                typeof(LookLayerSettings),
                typeof(TurnLayerSettings),
                typeof(IkLayerSettings)
            }));

            AdsLayerSettings ads = profile.Layers.OfType<AdsLayerSettings>().Single();
            PoseSamplerLayerSettings sampler = profile.Layers.OfType<PoseSamplerLayerSettings>().Single();
            AdditiveLayerSettings additive = profile.Layers.OfType<AdditiveLayerSettings>().Single();
            Assert.That(ads.WeaponIkBone.Name, Is.EqualTo("IK WeaponBone"));
            Assert.That(ads.AimTargetBone.Name, Is.EqualTo("Camera"));
            Assert.That(ads.CameraBlend, Is.EqualTo(1f));
            Assert.That(sampler.UseReferenceHandTargets, Is.True);
            Assert.That(sampler.DefaultWeaponBoneWeight, Is.EqualTo(1f));
            Assert.That(additive.AdditiveBone.Name, Is.EqualTo("WeaponBoneAdditive"));
        }

        [Test]
        public void Ak12RecoilProfile_KeepsCameraAndViewRecoilAtFullStrengthDuringAds()
        {
            RecoilProfile profile = AssetDatabase.LoadAssetAtPath<RecoilProfile>(
                "Assets/Art/Weapon/Profile/AK12/AK12RecoilProfile.asset");
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.AdsScalar, Is.EqualTo(0.3f));

            GameObject hipRoot = new GameObject("HipRecoilPawn");
            GameObject adsRoot = new GameObject("AdsRecoilPawn");
            try
            {
                Pawn hip = new Pawn(hipRoot);
                Pawn ads = new Pawn(adsRoot);
                hip.BindRecoilProfile(profile);
                ads.BindRecoilProfile(profile);
                hip.Recoil.ApplySuccessfulShot(false);
                ads.Recoil.ApplySuccessfulShot(true);

                Assert.That(ads.RecoilRotationOffsetDegrees, Is.EqualTo(hip.RecoilRotationOffsetDegrees));
                Assert.That(ads.CameraShakeSample, Is.EqualTo(hip.CameraShakeSample));
                Assert.That(ads.WeaponRecoilPose.position, Is.EqualTo(hip.WeaponRecoilPose.position));
                Assert.That(Quaternion.Angle(ads.WeaponRecoilPose.rotation, hip.WeaponRecoilPose.rotation), Is.LessThan(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(hipRoot);
                Object.DestroyImmediate(adsRoot);
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z)
                && !float.IsNaN(value.w) && !float.IsInfinity(value.w);
        }
    }
}
