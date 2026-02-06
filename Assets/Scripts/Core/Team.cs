using System;

namespace TUA.Core
{
    [Serializable]
    public class Team
    {
        #region Properties
        public string Name { get; set; }
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
        #endregion
    }
}
