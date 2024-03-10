using System;
using NetBuff.Components;
using NetBuff.Misc;
using SolarBuff.Data;

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
            WithValues(levelIndex);
        }

        public override void OnSpawned(bool isRetroactive)
        {
            if (!HasAuthority) 
                return;
            
            var profile = SaveManager.GetCurrentProfile();
            if (profile.Body.ContainsKey("level"))
                levelIndex.Value = profile.Body["level"];
            
            Instance = this;
            levels[levelIndex.Value].SetActive(true);
        }
        
        public void ChangeLevel(int level)
        {
            if (!HasAuthority)
                return;
            levels[levelIndex.Value].SetActive(false);
            levelIndex.Value = level;
            levels[levelIndex.Value].SetActive(true);
            
            var profile = SaveManager.GetCurrentProfile();
            profile.Body["level"] = levelIndex.Value;
            #pragma warning disable CS4014
            SaveManager.Save();
            #pragma warning restore CS4014
        }
    }
}