#if UNITY_EDITOR || DEVELOPMENT_BUILD || UNITY_STANDALONE
using UnityEngine;
using UnityEngine.InputSystem;

namespace CGame
{
    /// <summary>
    /// 为角色武器动画验收提供独立快捷键，不参与正式输入或启动流程。
    /// </summary>
    public sealed class WeaponAnimationTestInput : MonoBehaviour
    {
        private const string RuntimeObjectName = "[WeaponAnimationTestInput]";
        private static readonly WeaponId KnifeWeaponId = new WeaponId("knife");
        private static readonly WeaponId RifleWeaponId = new WeaponId("rifle");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateRuntimeInput()
        {
            if (FindObjectOfType<WeaponAnimationTestInput>() != null)
            {
                return;
            }

            var gameObject = new GameObject(RuntimeObjectName);
            DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<WeaponAnimationTestInput>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.tKey.wasPressedThisFrame)
            {
                return;
            }

            PawnHost pawnHost = FindObjectOfType<PawnHost>();
            Controller controller = pawnHost?.Pawn?.Controller;
            if (controller == null)
            {
                Debug.LogWarning("[WeaponAnimationTestInput] T ignored because no controlled Pawn is ready.");
                return;
            }

            WeaponId currentWeaponId =
                controller.WeaponRuntime.Snapshot.EquippedWeaponId;
            WeaponId targetWeaponId = currentWeaponId == RifleWeaponId
                ? KnifeWeaponId
                : RifleWeaponId;
            WeaponSwitchRequestResult result =
                controller.RequestSwitchWeapon(
                    targetWeaponId,
                    out _);
            if (result == WeaponSwitchRequestResult.Started)
            {
                Debug.Log(
                    "[WeaponAnimationTestInput] T requested weapon switch "
                    + $"{currentWeaponId} -> {targetWeaponId}.");
                return;
            }

            Debug.LogWarning(
                "[WeaponAnimationTestInput] T weapon switch was rejected: "
                + result);
        }
    }
}
#endif
