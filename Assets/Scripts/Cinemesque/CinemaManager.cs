using System;
using TMPro;

namespace SolarBuff.Cinemesque
{
    public class CinemaManager : SingletonBehaviour<CinemaManager>
    {
        /// <summary>
        /// Used to display dialog
        /// </summary>
        public TMP_Text labelDialog;
        
        /// <summary>
        /// Used to display any other subtitles
        /// </summary>
        public TMP_Text labelExternal;
        
        [Serializable]
        public class Text
        {
            public int slot;
            public float duration = 0f;
            public string text;
        }
    }
}