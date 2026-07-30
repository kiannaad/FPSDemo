using UnityEngine;

namespace CGame
{
    public sealed class FirstPersonCameraAnchor : MonoBehaviour, IFirstPersonCameraTarget, IFirstPersonCameraLocomotionSource
    {
        [SerializeField]
        private float localEyeHeight = 1.53328f;

        [SerializeField]
        private float localEyeForward = 0.06341f;

        [SerializeField]
        private float localEyeRight = 0.01689f;

        [SerializeField]
        private Vector3 animatedHeadCameraPosition =
            new Vector3(0.012f, 0.059f, 0.024f);

        private CharacterPhysicsMotor characterMotor;
        private Transform animatedHead;

        public Vector3 Position => animatedHead != null
            ? animatedHead.position
              + animatedHead.rotation * animatedHeadCameraPosition
            : transform.TransformPoint(
                localEyeRight,
                localEyeHeight,
                localEyeForward);
        public Quaternion Rotation => animatedHead != null
            ? animatedHead.rotation
              * Quaternion.Euler(-90f, 0f, 0f)
            : transform.rotation;
        public bool IsValid => this != null && gameObject.activeInHierarchy;
        public float LocalEyeHeight => localEyeHeight;
        public float LocalEyeForward => localEyeForward;
        public float LocalEyeRight => localEyeRight;
        public bool UsesAnimatedHeadMount => animatedHead != null;
        public CameraLocomotionSample LocomotionSample
        {
            get
            {
                if (characterMotor == null)
                {
                    return CameraLocomotionSample.Idle;
                }

                Vector3 velocity = characterMotor.Velocity;
                float verticalSpeed = Vector3.Dot(velocity, characterMotor.CharacterUp);
                float horizontalSpeed = Vector3.ProjectOnPlane(velocity, characterMotor.CharacterUp).magnitude;
                return new CameraLocomotionSample(
                    horizontalSpeed,
                    verticalSpeed,
                    characterMotor.GroundingStatus.IsStableOnGround);
            }
        }

        private void Awake()
        {
            characterMotor = GetComponentInParent<CharacterPhysicsMotor>();
            Transform searchRoot =
                characterMotor != null
                    ? characterMotor.transform
                    : transform.root;
            Transform[] transforms =
                searchRoot.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index].name == "Head")
                {
                    animatedHead = transforms[index];
                    break;
                }
            }
        }
    }
}
