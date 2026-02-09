using System;
using UnityEngine;

namespace TUA.Core
{
    [Serializable]
    public class Team
    {
        #region Properties
        public string Name { get; set; }
        public Color Color { get; set; }
        #endregion

        #region Constructors
        public Team()
        {
            Name = string.Empty;
        }

        public Team(string name)
        {
            Name = name;
        }

        public Team(string name, Color color)
        {
            Name = name;
            Color = color;
        }
        #endregion
    }
}
