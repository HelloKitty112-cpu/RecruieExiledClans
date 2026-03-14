using System;
using System.Runtime.CompilerServices;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v1;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using TaleWorlds.Localization;
namespace JoinMyKingdom
{

    public class AIClansAutoJoin : AttributeGlobalSettings<AIClansAutoJoin>
    {
        public override string Id
        {
            get
            {
                return "Recruit Exiled Clans by Pathfinder";
            }
        }
        public override string FormatType
        {
            get
            {
                return "json";
            }
        }
        public override string DisplayName
        {
            get
            {
                return new TextObject("{=AIClansAutoJoin}RecruitExiledClans", null).ToString();
            }
        }
        [SettingPropertyBool("{=AIClansAutoJoinD}Enable AI Clans AutoJoin Behavior", RequireRestart = false)]
        [SettingPropertyGroup("{=AIClansAutoJoin}RecruitExiledClans")]
        public bool autojoinon { get; set; } = true;
        [SettingPropertyBool("{=debugwerec}Output Test Information", RequireRestart = true)]
        [SettingPropertyGroup("{=AIClansAutoJoin}RecruitExiledClans")]
        public bool debuglog { get; set; } = false;


    }
}
