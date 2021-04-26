using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UABEAvalonia.Plugins
{
    public class UABEAPluginMenuInfo
    {
        public readonly PluginInfo pluginInf;
        public readonly UABEAPluginOption pluginOpt;
        public readonly string displayName;

        public UABEAPluginMenuInfo(PluginInfo pluginInf, UABEAPluginOption pluginOpt, string displayName)
        {
            this.pluginInf = pluginInf;
            this.pluginOpt = pluginOpt;
            this.displayName = displayName;
        }

        public override string ToString()
        {
            return displayName;
        }
    }
}
