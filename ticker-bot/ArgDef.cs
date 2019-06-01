using System;
using System.Collections.Generic;

namespace TwitchTicker {
    
    public abstract class ArgDef {
        public string Name {
            get; set;
        }

        public string ShortName {
            get; set;
        }

        public bool Matches(string token) {
            string longToken = "--" + Name;
            string shortToken = "-" + ShortName;
            return (token == longToken || token == shortToken);
        }
    }

    public class ArgDefFlag : ArgDef {
        public bool Value {
            get; set;
        }

        public ArgDefFlag(string name, string shortName = "") {
            Name = name;
            ShortName = shortName;
            Value = false;
        }
    }

    public class ArgDefKeyValue : ArgDef {
        public string Value {
            get; set;
        }

        public ArgDefKeyValue(string name, string shortName = "") {
            Name = name;
            ShortName = shortName;
            Value = String.Empty;
        }
    }

    public class ArgDefEqualityComparer : IEqualityComparer<ArgDef>
    {
        public bool Equals(ArgDef x, ArgDef y)
        {
            return (x == null && y == null) || (x.Name == y.Name);
        }

        public int GetHashCode(ArgDef obj)
        {
            return obj.Name.GetHashCode();
        }
    }
}