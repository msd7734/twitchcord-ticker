using System;
using System.Collections.Generic;
using System.Linq;

namespace TwitchTicker {
    public class ArgParse {
        // This could be done with a span or pointers or type-based subsets or someshit,
        //  but this is to simplify our lives, not create an optimized library...
        private HashSet<ArgDef> _argDefs;
        private HashSet<ArgDefFlag> _flagDefs;
        private HashSet<ArgDefKeyValue> _keyValDefs;

        public ArgParse() {
            _argDefs = new HashSet<ArgDef>(new ArgDefEqualityComparer());
            _flagDefs = new HashSet<ArgDefFlag>(new ArgDefEqualityComparer());
            _keyValDefs = new HashSet<ArgDefKeyValue>(new ArgDefEqualityComparer());
        }

        public void AddDefs (params ArgDef[] argDefs) {
            foreach (ArgDef argDef in argDefs) {
                Type t = argDef.GetType();
                if (t.Equals(typeof(ArgDefFlag))) {
                    Add((ArgDefFlag)argDef);
                }
                else if (t.Equals(typeof(ArgDefKeyValue))) {
                    Add((ArgDefKeyValue)argDef);
                }
                else {
                    throw new ArgumentException($"Unknown argument def implementation {t.ToString()}");
                }
            }
        }

        public void Add(ArgDefFlag flag) {
            // Don't add the flag if its identifier is already an argument
            if (_argDefs.Add(flag)) {
                _flagDefs.Add(flag);
            }
        }

        public void Add(ArgDefKeyValue kv) {
            // Don't add the key if its identifier is already an argument
            if (_argDefs.Add(kv)) {
                _keyValDefs.Add(kv);
            }
        }

        public void Parse(string[] args) {
            for (int i = 0; i < args.Length; ++i) {
                string token = args[i];
                ArgDef matchedDef = _argDefs.FirstOrDefault(x => x.Matches(token));
                if (matchedDef != null) {
                    Type t = matchedDef.GetType();
                    if (t.Equals(typeof(ArgDefFlag))) {
                        ArgDefFlag flag;
                        _flagDefs.TryGetValue((ArgDefFlag)matchedDef, out flag);
                        flag.Value = true;
                    }
                    else if (t.Equals(typeof(ArgDefKeyValue))) {
                        ArgDefKeyValue kv;
                        _keyValDefs.TryGetValue((ArgDefKeyValue)matchedDef, out kv);

                        // Handle if a key-value argument is at the end of the arg list,
                        //  but the user did not give a value
                        if (i+1 < args.Length) {
                            kv.Value = args[i+1];
                            i += 1;
                        }
                        
                    }
                    else {
                        throw new ArgumentException($"Unknown argument def implementation {t.ToString()}");
                    }
                }
            }
        }

        public bool GetFlag(string name) {
            ArgDefFlag res;
            if (_flagDefs.TryGetValue(new ArgDefFlag(name), out res)) {
                return res.Value;
            }
            return false;
        }

        public string GetValue(string name) {
            ArgDefKeyValue res;
            if (_keyValDefs.TryGetValue(new ArgDefKeyValue(name), out res)) {
                return res.Value;
            }
            return String.Empty;
        }
    }
}