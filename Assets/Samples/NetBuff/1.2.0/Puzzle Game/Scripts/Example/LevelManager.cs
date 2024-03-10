using System;
using NetBuff.Components;
using NetBuff.Misc;
using SolarBuff.Data;
using UnityEngine;

namespace ExamplePlatformer
{
    public class LevelManager : NetworkBehaviour
    {
        private static LevelManager _instance;
        
        public OrbitCamera[] orbitCameras;
        
        public static LevelManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindAnyObjectByType<LevelManager>();
                return _instance;
            }
            private set => _instance = value;
        }

        public IntNetworkValue levelIndex = new IntNetworkValue(0, NetworkValue.ModifierType.Server);
        public NetworkIdentity[] levels;

        private void OnEnable()
        {
            var profile = SaveManager.GetCurrentProfile();
            if (profile.Body.ContainsKey("level"))
                levelIndex = new IntNetworkValue(profile.Body["level"], NetworkValue.ModifierType.Server);

            WithValues(levelIndex);
            levelIndex.OnValueChanged += OnChangeLevel;
        }

        private void OnChangeLevel(int oldValue, int newValue)
        {
            var profile = SaveManager.GetCurrentProfile();
            Debug.Log("Changing level from " + oldValue + " to " + newValue);
            profile.Body["level"] = newValue;
            #pragma warning disable CS4014
            SaveManager.Save();
            #pragma warning restore CS4014
            
            if (!HasAuthority)
                return;
            levels[oldValue].SetActive(false);
            levels[newValue].SetActive(true);
        }

        public override void OnSpawned(bool isRetroactive)
        {
            if (!HasAuthority) 
                return;
            
            Instance = this;
            levels[levelIndex.Value].SetActive(true);
        }
        
        public void ChangeLevel(int level)
        {
            if (!HasAuthority)
                return;
            
            levelIndex.Value = level;
        }
    }
}