using System.Collections.Generic;
using System.Linq;
using CGame;
using CGame.GameplayTags;
using CGame.InventoryEquipment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

public static class SampleSceneInputTagSetup
{
    private const string PawnFolder = "Assets/Settings/Gameplay/SampleScene/PawnConfig";
    private const string InputConfigFolder = "Assets/Settings/Gameplay/SampleScene/InputConfig";
    private const string SourcePath = InputConfigFolder + "/SampleInputTagSource.asset";
    private const string ConfigPath = InputConfigFolder + "/SampleInputTagConfig.asset";
    private const string ProfilePath = InputConfigFolder + "/SampleInputProfile.asset";

    [MenuItem("CGame/Setup/SampleScene InputTag Ability")]
    private static void Configure()
    {
        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Data/Input/PlayerInput.inputactions");
        PlayerStateDefinition playerState = AssetDatabase.LoadAssetAtPath<PlayerStateDefinition>(PawnFolder + "/SamplePlayerStateDefinition.asset");
        if (actions == null || playerState == null) throw new System.InvalidOperationException("SampleScene input or PlayerStateDefinition assets are missing.");

        GameplayTag fire = CreateTag("InputTag.Weapon.Fire");
        GameplayTag reload = CreateTag("InputTag.Weapon.Reload");
        GameplayTag melee = CreateTag("InputTag.Weapon.Melee");
        GameplayTag aim = CreateTag("InputTag.Weapon.Aim");
        GameplayTagSource source = CreateOrLoadSource();
        InputTagConfig config = CreateOrLoadConfig(actions, fire, reload, melee, aim);
        InputProfile profile = CreateOrLoadProfile(actions, config);
        playerState.SetInputProfile(profile);
        EditorUtility.SetDirty(playerState);

        GameplayTagConfig sceneConfig = Object.FindFirstObjectByType<GameplayTagConfig>();
        if (sceneConfig == null) throw new System.InvalidOperationException("SampleScene has no GameplayTagConfig.");
        if (!sceneConfig.Sources.Contains(source)) sceneConfig.SetDefinition(sceneConfig.Sources.Concat(new[] { source }), sceneConfig.Redirects);
        EditorUtility.SetDirty(sceneConfig);

        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(sceneConfig.gameObject.scene);
        Debug.Log("SampleScene InputTag Ability setup complete.");
    }

    private static GameplayTagSource CreateOrLoadSource()
    {
        GameplayTagSource source = AssetDatabase.LoadAssetAtPath<GameplayTagSource>(SourcePath);
        if (source == null)
        {
            source = ScriptableObject.CreateInstance<GameplayTagSource>();
            AssetDatabase.CreateAsset(source, SourcePath);
        }

        source.SetDefinition("SampleInput", new[]
        {
            new GameplayTagSourceNode("InputTag", false, children: new[]
            {
                new GameplayTagSourceNode("Weapon", false, children: new[]
                {
                    new GameplayTagSourceNode("Fire", true), new GameplayTagSourceNode("Reload", true),
                    new GameplayTagSourceNode("Melee", true), new GameplayTagSourceNode("Aim", true)
                })
            })
        });
        EditorUtility.SetDirty(source);
        return source;
    }

    private static InputTagConfig CreateOrLoadConfig(InputActionAsset actions, GameplayTag fire, GameplayTag reload, GameplayTag melee, GameplayTag aim)
    {
        InputTagConfig config = AssetDatabase.LoadAssetAtPath<InputTagConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<InputTagConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }

        InputActionMap map = actions.FindActionMap("Player", true);
        config.SetBindings(new[]
        {
            new InputTagBinding(CreateReference(config, map.FindAction("Fire", true)), fire),
            new InputTagBinding(CreateReference(config, map.FindAction("Reload", true)), reload),
            new InputTagBinding(CreateReference(config, map.FindAction("Melee", true)), melee),
            new InputTagBinding(CreateReference(config, map.FindAction("Aim", true)), aim)
        });
        EditorUtility.SetDirty(config);
        return config;
    }

    private static InputProfile CreateOrLoadProfile(InputActionAsset actions, InputTagConfig config)
    {
        InputProfile profile = AssetDatabase.LoadAssetAtPath<InputProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<InputProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        profile.SetConfiguration(actions, actions.FindActionMap("Player", true), config);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static InputActionReference CreateReference(Object owner, InputAction action)
    {
        InputActionReference reference = InputActionReference.Create(action);
        reference.name = action.name + "InputTagReference";
        AssetDatabase.AddObjectToAsset(reference, owner);
        return reference;
    }

    private static GameplayTag CreateTag(string value)
    {
        if (!GameplayTag.TryCreateSerialized(value, out GameplayTag tag)) throw new System.InvalidOperationException(value);
        return tag;
    }
}
