using System;
using UnityEngine;
using System.Collections.Generic;
using CGame.GameplayTags;

namespace CGame
{
    public class GameLauncher : Singleton<GameLauncher>
    {
        private readonly Queue<ILaunchStep> stepQueue = new Queue<ILaunchStep>();
        private ILaunchStep currentStep;
        private long startTime;
        public long StartTime => startTime;

        public void InitGameStep()
        {
            stepQueue.Enqueue(new PreSourceStep());
            stepQueue.Enqueue(new EnterStep());
            stepQueue.Enqueue(new CharacterTestStep());

            // 记录当前时间戳
            DateTime utcNow = DateTime.UtcNow;
            startTime = ((DateTimeOffset)utcNow).ToUnixTimeMilliseconds();
        }

        public void ReturnLoginPanel()
        {
            stepQueue.Clear();
            NextFun();
        }

        public void NextFun()
        {
            if (currentStep == null)
            {
                if (stepQueue.Count > 0)
                {
                    ILaunchStep nextFun = stepQueue.Dequeue();
                    Debug.Log(
                        $"进入{nextFun.GetType().FullName}时间: {((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds() - startTime}");
                    nextFun.Enter();
                    currentStep = nextFun;
                }
            }
            else
            {
                currentStep.Exit();
                Debug.Log(
                    $"退出{currentStep.GetType().FullName}时间: {((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds() - startTime}");
                if (stepQueue.Count > 0)
                {
                    ILaunchStep nextFun = stepQueue.Dequeue();
                    nextFun.Enter();
                    Debug.Log(
                        $"进入{nextFun.GetType().FullName}时间: {((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds() - startTime}");
                    currentStep = nextFun;
                }
                else
                {
                    currentStep = null;
                }
            }
        }

        public void Update()
        {
            if (currentStep != null)
            {
                // 检查步骤是否完成
                if (currentStep.Update())
                {
                    // 执行下一个步骤
                    NextFun();
                }
            }
        }
    }
    
    public class GameLauncherMgr : MonoBehaviour
    {
        [SerializeField] private GameplayTagConfig gameplayTagConfig;

        public bool GameplayTagsInitialized { get; private set; }

        public void Awake()
        {
            if (!InitializeGameplayTags())
            {
                return;
            }

            GameLauncher.Instance.InitGameStep();
            GameLauncher.Instance.NextFun();
        }

        public bool InitializeGameplayTags()
        {
            GameplayTagsInitialized = false;
            GameplayTagManager.Instance.Shutdown();

            if (gameplayTagConfig == null)
            {
                Debug.LogError("GameLauncherMgr requires a GameplayTagConfig reference before launch.", this);
                return false;
            }

            GameplayTagRegistryBuildResult result = GameplayTagManager.Instance.Initialize(
                gameplayTagConfig.Sources,
                gameplayTagConfig.Redirects);
            if (!result.Succeeded)
            {
                foreach (GameplayTagRegistryError error in result.Errors)
                {
                    Debug.LogError($"GameplayTag initialization failed: {error}", this);
                }

                return false;
            }

            GameplayTagsInitialized = true;
            return true;
        }

        public void Update()
        {
            if (GameplayTagsInitialized)
            {
                GameLauncher.Instance.Update();
            }
        }

        public void OnDestroy()
        {
            if (GameplayTagsInitialized)
            {
                GameplayTagManager.Instance.Shutdown();
                GameplayTagsInitialized = false;
            }
        }
    }
}
