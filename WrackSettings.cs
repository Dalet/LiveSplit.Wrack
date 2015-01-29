using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
using System.Xml;

namespace LiveSplit.Wrack
{
    public partial class WrackSettings : UserControl
    {
        public bool AutoStart { get; set; }
        public bool AutoReset { get; set; }
        public bool AutoSplit { get; set; }

        public const string MAP_PREFIX = "map_";
        public Dictionary<string, bool> Maps { get; set; }

        private const bool DEFAULT_AUTOSTART = true;
        private const bool DEFAULT_AUTORESET = true;
        private const bool DEFAULT_AUTOSPLIT = true;

        public WrackSettings()
        {
            InitializeComponent();

            this.chkAutoStart.DataBindings.Add("Checked", this, "AutoStart", false, DataSourceUpdateMode.OnPropertyChanged);
            this.chkAutoReset.DataBindings.Add("Checked", this, "AutoReset", false, DataSourceUpdateMode.OnPropertyChanged);
            this.chkAutoSplit.DataBindings.Add("Checked", this, "AutoSplit", false, DataSourceUpdateMode.OnPropertyChanged);

            // defaults
            this.AutoStart = DEFAULT_AUTOSTART;
            this.AutoReset = DEFAULT_AUTORESET;
            this.AutoSplit = DEFAULT_AUTOSPLIT;
        }


        public XmlNode GetSettings(XmlDocument doc)
        {
            XmlElement settingsNode = doc.CreateElement("Settings");

            settingsNode.AppendChild(ToElement(doc, "Version", Assembly.GetExecutingAssembly().GetName().Version.ToString(3)));
            settingsNode.AppendChild(ToElement(doc, "AutoStart", this.AutoStart));
            settingsNode.AppendChild(ToElement(doc, "AutoReset", this.AutoReset));
            settingsNode.AppendChild(ToElement(doc, "AutoSplit", this.AutoSplit));

            return settingsNode;
        }

        public void SetSettings(XmlNode settings)
        {
            var element = (XmlElement)settings;

            this.AutoStart = ParseBool(settings, "AutoStart", DEFAULT_AUTOSTART);
            this.AutoReset = ParseBool(settings, "AutoReset", DEFAULT_AUTORESET);
            this.AutoSplit = ParseBool(settings, "AutoSplit", DEFAULT_AUTOSPLIT);
        }

        static bool ParseBool(XmlNode settings, string setting, bool default_ = false)
        {
            bool val;
            return settings[setting] != null ?
                (Boolean.TryParse(settings[setting].InnerText, out val) ? val : default_)
                : default_;
        }

        static XmlElement ToElement<T>(XmlDocument document, string name, T value)
        {
            XmlElement str = document.CreateElement(name);
            str.InnerText = value.ToString();
            return str;
        }

    }
}
