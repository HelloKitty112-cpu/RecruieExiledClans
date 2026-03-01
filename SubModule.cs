using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Actions;
using MCM.Abstractions.Base.Global;

namespace JoinMyKingdom
{
    public class SubModule : MBSubModuleBase
    {
        protected override void InitializeGameStarter(Game game, IGameStarter starterObject)
        {
            base.InitializeGameStarter(game, starterObject);
            CampaignGameStarter starter = starterObject as CampaignGameStarter;
            var existingBehaviors = Campaign.Current.GetCampaignBehaviors<RecruitExiledClans>();
            if (starter != null && !existingBehaviors.Any())
            {
                starter.AddBehavior(new RecruitExiledClans (GlobalSettings<AIClansAutoJoin>.Instance));
            }
        }
        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            InformationManager.DisplayMessage(new InformationMessage("Recruit Exiled Clans (v1.3.X) loaded", new Color(0.2f, 0.6f, 1f)));
        }
    }
}
