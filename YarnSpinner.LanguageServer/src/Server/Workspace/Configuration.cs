﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace YarnLanguageServer
{
    internal class Configuration
    {
        // This whole setup will probably get reworked once I get a days away from staring at Visual Studio configuaration documentation
        private bool csharplookup = false;

        public bool CSharpLookup
        {
            get => csharplookup;
            set
            {
                if (csharplookup != value)
                {
                    csharplookup = value;
                }
            }
        }

        public static class Defaults
        {
            public const float DidYouMeanThreshold = 0.24f;
        }

        public float DidYouMeanThreshold { get; set; } = Defaults.DidYouMeanThreshold;
        public bool OnlySuggestDeclaredVariables { get; set; } = true;

        public void Initialize(JArray values)
        {
            if (values == null) { return; }
            try
            {
                // todo: populating itself, not the cleanest way to do this
                JsonSerializer.CreateDefault().Populate(values[0].CreateReader(), this);
            }
            catch (Exception) { }
        }

        public void Update(JToken wrappedValue)
        {
            if (wrappedValue == null) { return; }
            try
            {
                // todo clean up this late night code
                var value = wrappedValue.Children().FirstOrDefault()?.Children().FirstOrDefault();

                if (value != null)
                {
                    JsonSerializer.CreateDefault().Populate(value.CreateReader(), this);
                }
            }
            catch (Exception) { }
        }
    }
}
