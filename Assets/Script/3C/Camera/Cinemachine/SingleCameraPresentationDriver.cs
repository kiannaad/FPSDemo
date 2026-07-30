using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CGame
{
    [DefaultExecutionOrder(32000)]
    public sealed class SingleCameraPresentationDriver : MonoBehaviour
    {
        private sealed class HeadlessMeshBinding
        {
            public HeadlessMeshBinding(
                SkinnedMeshRenderer renderer,
                Mesh originalMesh,
                Mesh headlessMesh)
            {
                Renderer = renderer;
                OriginalMesh = originalMesh;
                HeadlessMesh = headlessMesh;
            }

            public SkinnedMeshRenderer Renderer { get; }
            public Mesh OriginalMesh { get; }
            public Mesh HeadlessMesh { get; }
        }

        private Camera outputCamera;
        private CameraPose pose;
        private float fieldOfView;
        private bool hasFrame;
        private Transform ownerHead;
        private readonly List<HeadlessMeshBinding> ownerHeadMeshes =
            new List<HeadlessMeshBinding>();
        private bool hideOwnerHead;
        private bool ownerHeadHidden;

        public bool OwnerHeadWasHiddenForRender { get; private set; }
        public int OwnerHeadMeshCount => ownerHeadMeshes.Count;

        public void SetFrame(CameraPose pose, float fieldOfView)
        {
            this.pose = pose;
            this.fieldOfView = fieldOfView;
            hasFrame = true;
            Present();
        }

        public void ClearFrame()
        {
            hasFrame = false;
        }

        public void SetOwnerHead(
            Transform head,
            bool hideDuringRender)
        {
            ReleaseOwnerHeadMeshes();
            ownerHead = head;
            hideOwnerHead = hideDuringRender;
            BuildOwnerHeadMeshes();
            OwnerHeadWasHiddenForRender = false;
        }

        public void SetOwnerHeadHidden(bool hidden)
        {
            hideOwnerHead = hidden;
            if (!hidden)
            {
                RestoreOwnerHead();
                OwnerHeadWasHiddenForRender = false;
            }
        }

        private void Awake()
        {
            outputCamera = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering +=
                HandleBeginCameraRendering;
            RenderPipelineManager.endCameraRendering +=
                HandleEndCameraRendering;
        }

        private void LateUpdate()
        {
            Present();
        }

        private void OnPreCull()
        {
            Present();
            HideOwnerHead();
            OwnerHeadWasHiddenForRender = ownerHeadHidden;
        }

        private void OnPostRender()
        {
            RestoreOwnerHead();
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -=
                HandleBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -=
                HandleEndCameraRendering;
            RestoreOwnerHead();
        }

        private void OnDestroy()
        {
            ReleaseOwnerHeadMeshes();
        }

        private void Present()
        {
            if (!hasFrame || outputCamera == null)
            {
                return;
            }

            transform.SetPositionAndRotation(pose.Position, pose.Rotation);
            outputCamera.fieldOfView = fieldOfView;
        }

        private void HandleBeginCameraRendering(
            ScriptableRenderContext context,
            Camera camera)
        {
            if (camera != outputCamera)
            {
                return;
            }

            Present();
            HideOwnerHead();
            OwnerHeadWasHiddenForRender = ownerHeadHidden;
        }

        private void HandleEndCameraRendering(
            ScriptableRenderContext context,
            Camera camera)
        {
            if (camera == outputCamera)
            {
                RestoreOwnerHead();
            }
        }

        private void HideOwnerHead()
        {
            if (!hideOwnerHead
                || ownerHead == null
                || ownerHeadMeshes.Count == 0
                || ownerHeadHidden)
            {
                return;
            }

            bool swappedAnyMesh = false;
            foreach (HeadlessMeshBinding binding in ownerHeadMeshes)
            {
                if (binding.Renderer == null
                    || binding.Renderer.sharedMesh != binding.OriginalMesh)
                {
                    continue;
                }

                binding.Renderer.sharedMesh = binding.HeadlessMesh;
                swappedAnyMesh = true;
            }

            ownerHeadHidden = swappedAnyMesh;
        }

        private void RestoreOwnerHead()
        {
            if (!ownerHeadHidden)
            {
                return;
            }

            foreach (HeadlessMeshBinding binding in ownerHeadMeshes)
            {
                if (binding.Renderer != null
                    && binding.Renderer.sharedMesh == binding.HeadlessMesh)
                {
                    binding.Renderer.sharedMesh = binding.OriginalMesh;
                }
            }

            ownerHeadHidden = false;
        }

        private void BuildOwnerHeadMeshes()
        {
            if (ownerHead == null)
            {
                return;
            }

            SkinnedMeshRenderer[] renderers =
                ownerHead.root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                if (TryCreateHeadlessMesh(renderer, ownerHead, out Mesh headlessMesh))
                {
                    ownerHeadMeshes.Add(
                        new HeadlessMeshBinding(
                            renderer,
                            renderer.sharedMesh,
                            headlessMesh));
                }
            }
        }

        private void ReleaseOwnerHeadMeshes()
        {
            RestoreOwnerHead();
            foreach (HeadlessMeshBinding binding in ownerHeadMeshes)
            {
                DestroyOwnedMesh(binding.HeadlessMesh);
            }

            ownerHeadMeshes.Clear();
            ownerHead = null;
            ownerHeadHidden = false;
            OwnerHeadWasHiddenForRender = false;
        }

        private static bool TryCreateHeadlessMesh(
            SkinnedMeshRenderer renderer,
            Transform head,
            out Mesh headlessMesh)
        {
            headlessMesh = null;
            if (renderer == null
                || renderer.sharedMesh == null
                || head == null)
            {
                return false;
            }

            Mesh sourceMesh = renderer.sharedMesh;
            BoneWeight[] boneWeights = sourceMesh.boneWeights;
            Transform[] bones = renderer.bones;
            if (boneWeights == null
                || boneWeights.Length != sourceMesh.vertexCount
                || bones == null
                || bones.Length == 0)
            {
                return false;
            }

            bool[] headBoneIndices = new bool[bones.Length];
            bool hasHeadBone = false;
            for (int index = 0; index < bones.Length; index++)
            {
                Transform bone = bones[index];
                bool belongsToHead =
                    bone != null
                    && (bone == head || bone.IsChildOf(head));
                headBoneIndices[index] = belongsToHead;
                hasHeadBone |= belongsToHead;
            }

            if (!hasHeadBone)
            {
                return false;
            }

            bool[] headVertices = new bool[sourceMesh.vertexCount];
            for (int index = 0; index < boneWeights.Length; index++)
            {
                headVertices[index] =
                    CalculateHeadWeight(
                        boneWeights[index],
                        headBoneIndices) >= 0.5f;
            }

            Mesh filteredMesh = Instantiate(sourceMesh);
            filteredMesh.name = sourceMesh.name + " (First Person Headless)";
            int removedTriangleCount = 0;
            for (int subMeshIndex = 0;
                subMeshIndex < sourceMesh.subMeshCount;
                subMeshIndex++)
            {
                int[] sourceTriangles =
                    sourceMesh.GetTriangles(subMeshIndex);
                var visibleTriangles =
                    new List<int>(sourceTriangles.Length);
                for (int triangleIndex = 0;
                    triangleIndex < sourceTriangles.Length;
                    triangleIndex += 3)
                {
                    int first = sourceTriangles[triangleIndex];
                    int second = sourceTriangles[triangleIndex + 1];
                    int third = sourceTriangles[triangleIndex + 2];
                    if (headVertices[first]
                        || headVertices[second]
                        || headVertices[third])
                    {
                        removedTriangleCount++;
                        continue;
                    }

                    visibleTriangles.Add(first);
                    visibleTriangles.Add(second);
                    visibleTriangles.Add(third);
                }

                filteredMesh.SetTriangles(
                    visibleTriangles,
                    subMeshIndex,
                    false);
            }

            if (removedTriangleCount == 0)
            {
                DestroyOwnedMesh(filteredMesh);
                return false;
            }

            filteredMesh.bounds = sourceMesh.bounds;
            headlessMesh = filteredMesh;
            return true;
        }

        private static float CalculateHeadWeight(
            BoneWeight boneWeight,
            bool[] headBoneIndices)
        {
            float weight = 0f;
            AddHeadWeight(
                ref weight,
                boneWeight.boneIndex0,
                boneWeight.weight0,
                headBoneIndices);
            AddHeadWeight(
                ref weight,
                boneWeight.boneIndex1,
                boneWeight.weight1,
                headBoneIndices);
            AddHeadWeight(
                ref weight,
                boneWeight.boneIndex2,
                boneWeight.weight2,
                headBoneIndices);
            AddHeadWeight(
                ref weight,
                boneWeight.boneIndex3,
                boneWeight.weight3,
                headBoneIndices);
            return weight;
        }

        private static void AddHeadWeight(
            ref float total,
            int boneIndex,
            float weight,
            bool[] headBoneIndices)
        {
            if (weight > 0f
                && boneIndex >= 0
                && boneIndex < headBoneIndices.Length
                && headBoneIndices[boneIndex])
            {
                total += weight;
            }
        }

        private static void DestroyOwnedMesh(Mesh mesh)
        {
            if (mesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(mesh);
            }
            else
            {
                DestroyImmediate(mesh);
            }
        }
    }
}
